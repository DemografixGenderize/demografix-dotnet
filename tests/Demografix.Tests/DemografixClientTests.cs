using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Demografix.Tests;

public class DemografixClientTests
{
    // ---- fixtures from INTERFACE.md section 5 ----

    private const string GenderizeSingle =
        "{ \"count\": 1352696, \"name\": \"peter\", \"gender\": \"male\", \"probability\": 1.0 }";

    private const string GenderizeCountry =
        "{ \"count\": 196601, \"name\": \"kim\", \"gender\": \"female\", \"country_id\": \"US\", \"probability\": 0.94 }";

    private const string GenderizeNull =
        "{ \"name\": \"xÿz\", \"gender\": null, \"probability\": 0.0, \"count\": 0 }";

    private const string AgifySingle =
        "{ \"count\": 311558, \"name\": \"michael\", \"age\": 57 }";

    private const string AgifyBatch =
        "[ { \"count\": 311558, \"name\": \"michael\", \"age\": 57 }, " +
        "  { \"count\": 55682,  \"name\": \"matthew\", \"age\": 48 } ]";

    private const string AgifyNull =
        "{ \"name\": \"xÿz\", \"age\": null, \"count\": 0 }";

    private const string NationalizeSingle =
        "{ \"count\": 100783, \"name\": \"nguyen\", " +
        "  \"country\": [ { \"country_id\": \"VN\", \"probability\": 0.891132 }, " +
        "                 { \"country_id\": \"MO\", \"probability\": 0.019031 } ] }";

    private const string NationalizeNull =
        "{ \"name\": \"xÿz\", \"country\": [], \"count\": 0 }";

    // ---- 1. single parse + quota.remaining == 24987 ----

    [Fact]
    public async Task Genderize_single_parses_fields_and_quota()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GenderizeSingle);
        using var client = TestClient.Create(handler);

        var result = await client.GenderizeAsync("peter");

        Assert.Equal("peter", result.Name);
        Assert.Equal("male", result.Gender);
        Assert.Equal(1.0, result.Probability);
        Assert.Equal(1352696, result.Count);
        Assert.Null(result.CountryId);
        Assert.Equal(25000, result.Quota.Limit);
        Assert.Equal(24987, result.Quota.Remaining);
        Assert.Equal(1314000, result.Quota.Reset);
    }

    [Fact]
    public async Task Agify_single_parses_fields_and_quota()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, AgifySingle);
        using var client = TestClient.Create(handler);

        var result = await client.AgifyAsync("michael");

        Assert.Equal("michael", result.Name);
        Assert.Equal(57, result.Age);
        Assert.Equal(311558, result.Count);
        Assert.Equal(24987, result.Quota.Remaining);
    }

    [Fact]
    public async Task Nationalize_single_parses_fields_and_quota()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, NationalizeSingle);
        using var client = TestClient.Create(handler);

        var result = await client.NationalizeAsync("nguyen");

        Assert.Equal("nguyen", result.Name);
        Assert.Equal(100783, result.Count);
        Assert.Equal(2, result.Country.Count);
        Assert.Equal("VN", result.Country[0].CountryId);
        Assert.Equal(0.891132, result.Country[0].Probability, 6);
        Assert.Equal("MO", result.Country[1].CountryId);
        Assert.Equal(24987, result.Quota.Remaining);
    }

    // ---- 2. batch parses results in order + quota ----

    [Fact]
    public async Task Agify_batch_parses_results_in_order_and_quota()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, AgifyBatch);
        using var client = TestClient.Create(handler);

        var batch = await client.AgifyBatchAsync(new[] { "michael", "matthew" });

        Assert.Equal(2, batch.Results.Count);
        Assert.Equal("michael", batch.Results[0].Name);
        Assert.Equal(57, batch.Results[0].Age);
        Assert.Equal("matthew", batch.Results[1].Name);
        Assert.Equal(48, batch.Results[1].Age);
        Assert.Equal(24987, batch.Quota.Remaining);

        // Repeated name[] params in input order.
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("name%5B%5D=michael", query);
        Assert.Contains("name%5B%5D=matthew", query);
        Assert.True(query.IndexOf("michael", StringComparison.Ordinal) < query.IndexOf("matthew", StringComparison.Ordinal));
    }

    // ---- 3. null prediction returns null / empty without error ----

    [Fact]
    public async Task Genderize_null_prediction_is_a_normal_result()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GenderizeNull);
        using var client = TestClient.Create(handler);

        var result = await client.GenderizeAsync("xÿz");

        Assert.Null(result.Gender);
        Assert.Equal(0.0, result.Probability);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Agify_null_prediction_is_a_normal_result()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, AgifyNull);
        using var client = TestClient.Create(handler);

        var result = await client.AgifyAsync("xÿz");

        Assert.Null(result.Age);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Nationalize_null_prediction_is_a_normal_result()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, NationalizeNull);
        using var client = TestClient.Create(handler);

        var result = await client.NationalizeAsync("xÿz");

        Assert.Empty(result.Country);
        Assert.Equal(0, result.Count);
    }

    // ---- 4. country_id round-trips into the request and back ----

    [Fact]
    public async Task Genderize_country_id_round_trips()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GenderizeCountry);
        using var client = TestClient.Create(handler);

        var result = await client.GenderizeAsync("kim", countryId: "us");

        // Into the request.
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("country_id=us", query);
        Assert.Contains("name=kim", query);

        // Back from the response (echoed uppercase by the server).
        Assert.Equal("female", result.Gender);
        Assert.Equal("US", result.CountryId);
        Assert.Equal(0.94, result.Probability);
    }

    [Fact]
    public async Task No_country_id_means_no_query_param()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GenderizeSingle);
        using var client = TestClient.Create(handler);

        await client.GenderizeAsync("peter");

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.DoesNotContain("country_id", query);
    }

    [Fact]
    public async Task Api_key_is_added_only_when_set()
    {
        var withKey = new FakeHandler(HttpStatusCode.OK, GenderizeSingle);
        using (var client = TestClient.Create(withKey, apiKey: "secret"))
        {
            await client.GenderizeAsync("peter");
            Assert.Contains("apikey=secret", withKey.LastRequest!.RequestUri!.Query);
        }

        var withoutKey = new FakeHandler(HttpStatusCode.OK, GenderizeSingle);
        using (var client = TestClient.Create(withoutKey))
        {
            await client.GenderizeAsync("peter");
            Assert.DoesNotContain("apikey", withoutKey.LastRequest!.RequestUri!.Query);
        }
    }

    [Fact]
    public async Task User_agent_is_sent_on_every_request()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GenderizeSingle);
        using var client = TestClient.Create(handler);

        await client.GenderizeAsync("peter");

        Assert.True(handler.LastRequest!.Headers.TryGetValues("User-Agent", out var values));
        Assert.Contains("demografix-csharp/0.1.0", string.Join(",", values!));
    }

    // ---- 5. batch of 11 raises ValidationException with NO HTTP call ----

    [Fact]
    public async Task Batch_over_ten_raises_validation_before_any_http_call()
    {
        var handler = new ThrowingHandler();
        using var client = TestClient.Create(handler);

        var eleven = new List<string>();
        for (var i = 0; i < 11; i++)
        {
            eleven.Add("name" + i);
        }

        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.GenderizeBatchAsync(eleven));
        Assert.Equal(0, handler.CallCount);
        Assert.Null(ex.Status);
        Assert.Null(ex.Quota);
    }

    [Fact]
    public async Task Null_name_throws_ArgumentNullException_before_any_http_call()
    {
        var handler = new ThrowingHandler();
        using var client = TestClient.Create(handler);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.GenderizeAsync(null!));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Null_names_throws_ArgumentNullException_before_any_http_call()
    {
        var handler = new ThrowingHandler();
        using var client = TestClient.Create(handler);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.GenderizeBatchAsync(null!));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Cancelled_token_cancels_the_request()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, GenderizeSingle);
        using var client = TestClient.Create(handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GenderizeAsync("peter", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Batch_of_ten_is_allowed()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "[]");
        using var client = TestClient.Create(handler);

        var ten = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            ten.Add("name" + i);
        }

        var batch = await client.GenderizeBatchAsync(ten);
        Assert.Empty(batch.Results);
        Assert.Equal(1, handler.CallCount);
    }

    // ---- 6. error status mapping carrying status, message, and (429) quota ----

    [Fact]
    public async Task Status_401_maps_to_AuthException()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "{ \"error\": \"Invalid API key\" }");
        using var client = TestClient.Create(handler);

        var ex = await Assert.ThrowsAsync<AuthException>(() => client.GenderizeAsync("peter"));
        Assert.Equal(401, ex.Status);
        Assert.Equal("Invalid API key", ex.Message);
    }

    [Fact]
    public async Task Status_402_maps_to_SubscriptionException()
    {
        var handler = new FakeHandler((HttpStatusCode)402, "{ \"error\": \"Subscription is not active\" }");
        using var client = TestClient.Create(handler);

        var ex = await Assert.ThrowsAsync<SubscriptionException>(() => client.AgifyAsync("michael"));
        Assert.Equal(402, ex.Status);
        Assert.Equal("Subscription is not active", ex.Message);
    }

    [Fact]
    public async Task Status_422_maps_to_ValidationException()
    {
        var handler = new FakeHandler((HttpStatusCode)422, "{ \"error\": \"Missing 'name' parameter\" }");
        using var client = TestClient.Create(handler);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.NationalizeAsync("peter"));
        Assert.Equal(422, ex.Status);
        Assert.Equal("Missing 'name' parameter", ex.Message);
    }

    [Fact]
    public async Task Status_429_maps_to_RateLimitException_with_quota()
    {
        var handler = new FakeHandler((HttpStatusCode)429, "{ \"error\": \"Request limit reached\" }");
        using var client = TestClient.Create(handler);

        var ex = await Assert.ThrowsAsync<RateLimitException>(() => client.GenderizeAsync("peter"));
        Assert.Equal(429, ex.Status);
        Assert.Equal("Request limit reached", ex.Message);
        Assert.NotNull(ex.Quota);
        Assert.Equal(24987, ex.Quota!.Remaining);
        Assert.Equal(1314000, ex.Quota.Reset);
    }

    [Fact]
    public async Task Other_non_2xx_maps_to_base_DemografixException()
    {
        var handler = new FakeHandler(HttpStatusCode.InternalServerError, "{ \"error\": \"boom\" }");
        using var client = TestClient.Create(handler);

        var ex = await Assert.ThrowsAsync<DemografixException>(() => client.GenderizeAsync("peter"));
        Assert.Equal(500, ex.Status);
        Assert.Equal("boom", ex.Message);
        // Confirm it is the base type, not a subclass.
        Assert.Equal(typeof(DemografixException), ex.GetType());
    }

    [Fact]
    public async Task Non_json_body_maps_to_TransportException()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "<html>not json</html>");
        using var client = TestClient.Create(handler);

        await Assert.ThrowsAsync<TransportException>(() => client.GenderizeAsync("peter"));
    }

    // A non-2xx response whose body is not JSON (e.g. an HTML 502 from a gateway) is a transport-level
    // failure, not a status-typed error. The body is decoded as JSON first regardless of status, so the
    // status never reaches the error map. The quota headers are still present, so they ride along.
    [Fact]
    public async Task NonJsonErrorBodyMapsToTransportException()
    {
        var handler = new FakeHandler(
            (HttpStatusCode)502,
            "<html>502 Bad Gateway</html>",
            FakeHandler.DefaultHeaders);
        using var client = TestClient.Create(handler);

        var ex = await Assert.ThrowsAsync<TransportException>(() => client.GenderizeAsync("peter"));
        Assert.Equal(502, ex.Status);
        Assert.NotNull(ex.Quota);
        Assert.Equal(24987, ex.Quota!.Remaining);
    }
}
