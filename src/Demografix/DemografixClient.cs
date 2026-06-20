using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Demografix;

/// <summary>
/// Client for the three Demografix services: genderize, agify, and nationalize. One instance covers all
/// three. The per-service hosts and the User-Agent are hardcoded constants, never options.
/// </summary>
public sealed class DemografixClient : IDisposable
{
    private const string Version = "0.1.0";
    private const string UserAgent = "demografix-csharp/" + Version;

    private const string GenderizeBase = "https://api.genderize.io";
    private const string AgifyBase = "https://api.agify.io";
    private const string NationalizeBase = "https://api.nationalize.io";

    private const int MaxBatch = 10;

    private readonly string? _apiKey;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    /// <summary>
    /// Constructs a client. <paramref name="apiKey"/> is optional; without it, requests go out on the free
    /// per-IP tier. <paramref name="timeout"/> defaults to ten seconds.
    /// </summary>
    public DemografixClient(string? apiKey = null, TimeSpan? timeout = null)
        : this(apiKey, timeout, handler: null)
    {
    }

    /// <summary>
    /// Internal transport seam. Tests pass a fake <see cref="HttpMessageHandler"/> that returns canned
    /// responses without hitting the network. The public constructor stays <c>new DemografixClient(apiKey)</c>.
    /// </summary>
    internal DemografixClient(string? apiKey, TimeSpan? timeout, HttpMessageHandler? handler)
    {
        _apiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey;

        // The client always owns its HttpClient. When a test injects a handler, the test keeps ownership of
        // the handler (disposeHandler: false) so it can be reused or inspected after the call.
        _http = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: false);
        _ownsHttp = true;

        _http.Timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    // ---- genderize ----

    /// <summary>
    /// Predicts the gender of a single name. Pass an optional <paramref name="countryId"/>
    /// (ISO 3166-1 alpha-2) to scope the prediction to one country. A null gender is a normal result, not an
    /// error.
    /// </summary>
    /// <param name="name">The name to classify.</param>
    /// <param name="countryId">Optional country to scope the prediction to; echoed back uppercase.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The prediction fields plus the response <see cref="Demografix.Quota"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="DemografixException">The request failed or the API returned a non-2xx status.</exception>
    public async Task<GenderizeResult> GenderizeAsync(
        string name,
        string? countryId = null,
        CancellationToken cancellationToken = default)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var (root, quota) = await GetSingleAsync(GenderizeBase, name, countryId, cancellationToken)
            .ConfigureAwait(false);
        var prediction = ParseGenderize(root);
        return new GenderizeResult(
            prediction.Name, prediction.Gender, prediction.Probability, prediction.Count, prediction.CountryId, quota);
    }

    /// <summary>
    /// Predicts the gender of up to ten names in one request, preserving input order. Pass an optional
    /// <paramref name="countryId"/> to scope the whole batch to one country.
    /// </summary>
    /// <param name="names">The names to classify; at most ten.</param>
    /// <param name="countryId">Optional country to scope the predictions to; echoed back uppercase.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The per-name predictions in input order plus one response <see cref="Demografix.Quota"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is null.</exception>
    /// <exception cref="ValidationException">More than ten names were supplied; raised before any HTTP call.</exception>
    /// <exception cref="DemografixException">The request failed or the API returned a non-2xx status.</exception>
    public async Task<Batch<GenderizePrediction>> GenderizeBatchAsync(
        IEnumerable<string> names,
        string? countryId = null,
        CancellationToken cancellationToken = default)
    {
        var (root, quota) = await GetBatchAsync(GenderizeBase, names, countryId, cancellationToken)
            .ConfigureAwait(false);
        var results = new List<GenderizePrediction>(root.GetArrayLength());
        foreach (var element in root.EnumerateArray())
        {
            results.Add(ParseGenderize(element));
        }
        return new Batch<GenderizePrediction>(results, quota);
    }

    // ---- agify ----

    /// <summary>
    /// Predicts the age of a single name. Pass an optional <paramref name="countryId"/>
    /// (ISO 3166-1 alpha-2) to scope the prediction to one country. A null age is a normal result, not an
    /// error.
    /// </summary>
    /// <param name="name">The name to classify.</param>
    /// <param name="countryId">Optional country to scope the prediction to; echoed back uppercase.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The prediction fields plus the response <see cref="Demografix.Quota"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="DemografixException">The request failed or the API returned a non-2xx status.</exception>
    public async Task<AgifyResult> AgifyAsync(
        string name,
        string? countryId = null,
        CancellationToken cancellationToken = default)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var (root, quota) = await GetSingleAsync(AgifyBase, name, countryId, cancellationToken)
            .ConfigureAwait(false);
        var prediction = ParseAgify(root);
        return new AgifyResult(
            prediction.Name, prediction.Age, prediction.Count, prediction.CountryId, quota);
    }

    /// <summary>
    /// Predicts the age of up to ten names in one request, preserving input order. Pass an optional
    /// <paramref name="countryId"/> to scope the whole batch to one country.
    /// </summary>
    /// <param name="names">The names to classify; at most ten.</param>
    /// <param name="countryId">Optional country to scope the predictions to; echoed back uppercase.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The per-name predictions in input order plus one response <see cref="Demografix.Quota"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is null.</exception>
    /// <exception cref="ValidationException">More than ten names were supplied; raised before any HTTP call.</exception>
    /// <exception cref="DemografixException">The request failed or the API returned a non-2xx status.</exception>
    public async Task<Batch<AgifyPrediction>> AgifyBatchAsync(
        IEnumerable<string> names,
        string? countryId = null,
        CancellationToken cancellationToken = default)
    {
        var (root, quota) = await GetBatchAsync(AgifyBase, names, countryId, cancellationToken)
            .ConfigureAwait(false);
        var results = new List<AgifyPrediction>(root.GetArrayLength());
        foreach (var element in root.EnumerateArray())
        {
            results.Add(ParseAgify(element));
        }
        return new Batch<AgifyPrediction>(results, quota);
    }

    // ---- nationalize ----

    /// <summary>
    /// Predicts the likely nationality of a single name, with up to five candidate countries in descending
    /// probability. An empty candidate list is a normal result, not an error. Nationalize does not take a
    /// country_id.
    /// </summary>
    /// <param name="name">The name to classify.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The prediction fields plus the response <see cref="Demografix.Quota"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="DemografixException">The request failed or the API returned a non-2xx status.</exception>
    public async Task<NationalizeResult> NationalizeAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var (root, quota) = await GetSingleAsync(NationalizeBase, name, countryId: null, cancellationToken)
            .ConfigureAwait(false);
        var prediction = ParseNationalize(root);
        return new NationalizeResult(prediction.Name, prediction.Country, prediction.Count, quota);
    }

    /// <summary>
    /// Predicts the likely nationality of up to ten names in one request, preserving input order. Nationalize
    /// does not take a country_id.
    /// </summary>
    /// <param name="names">The names to classify; at most ten.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The per-name predictions in input order plus one response <see cref="Demografix.Quota"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is null.</exception>
    /// <exception cref="ValidationException">More than ten names were supplied; raised before any HTTP call.</exception>
    /// <exception cref="DemografixException">The request failed or the API returned a non-2xx status.</exception>
    public async Task<Batch<NationalizePrediction>> NationalizeBatchAsync(
        IEnumerable<string> names,
        CancellationToken cancellationToken = default)
    {
        var (root, quota) = await GetBatchAsync(NationalizeBase, names, countryId: null, cancellationToken)
            .ConfigureAwait(false);
        var results = new List<NationalizePrediction>(root.GetArrayLength());
        foreach (var element in root.EnumerateArray())
        {
            results.Add(ParseNationalize(element));
        }
        return new Batch<NationalizePrediction>(results, quota);
    }

    // ---- request plumbing ----

    private async Task<(JsonElement Root, Quota Quota)> GetSingleAsync(
        string baseUrl,
        string name,
        string? countryId,
        CancellationToken cancellationToken)
    {
        var query = new StringBuilder();
        AppendParam(query, "name", name);
        AppendOptions(query, countryId);
        return await SendAsync(baseUrl, query.ToString(), JsonValueKind.Object, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(JsonElement Root, Quota Quota)> GetBatchAsync(
        string baseUrl,
        IEnumerable<string> names,
        string? countryId,
        CancellationToken cancellationToken)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        var list = new List<string>(names);
        if (list.Count > MaxBatch)
        {
            // Client-side guard: raised before any HTTP call.
            throw new ValidationException(
                $"A batch accepts at most {MaxBatch} names; received {list.Count}.", status: null, quota: null);
        }

        var query = new StringBuilder();
        foreach (var n in list)
        {
            AppendParam(query, "name[]", n);
        }
        AppendOptions(query, countryId);
        return await SendAsync(baseUrl, query.ToString(), JsonValueKind.Array, cancellationToken)
            .ConfigureAwait(false);
    }

    private void AppendOptions(StringBuilder query, string? countryId)
    {
        if (!string.IsNullOrEmpty(countryId))
        {
            AppendParam(query, "country_id", countryId!);
        }
        if (_apiKey is not null)
        {
            AppendParam(query, "apikey", _apiKey);
        }
    }

    private static void AppendParam(StringBuilder query, string key, string value)
    {
        query.Append(query.Length == 0 ? '?' : '&');
        query.Append(Uri.EscapeDataString(key));
        query.Append('=');
        query.Append(Uri.EscapeDataString(value));
    }

    private async Task<(JsonElement Root, Quota Quota)> SendAsync(
        string baseUrl,
        string query,
        JsonValueKind expectedSuccessKind,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/" + query);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces a request timeout as a cancellation that the caller did not request.
            throw new TransportException("Request timed out.", innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TransportException(ex.Message, innerException: ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            var quota = ParseQuota(response);

            // JSON-first: decode the body before looking at the status, matching the canonical SDK. A body
            // that is empty or not well-formed JSON is a transport-level failure regardless of status, so an
            // HTML 502 becomes a TransportException rather than a status-typed error.
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                throw new TransportException(
                    "Response body was not valid JSON.", status, quota, innerException: ex);
            }

            using (document)
            {
                var root = document.RootElement;

                // Only a well-formed JSON body proceeds to status -> error mapping.
                if (!response.IsSuccessStatusCode)
                {
                    throw MapError(status, root, quota);
                }

                // A success body that is valid JSON but the wrong shape (e.g. an object where the batch
                // expected an array) is structurally incompatible and maps to a transport failure.
                if (root.ValueKind != expectedSuccessKind)
                {
                    throw new TransportException(
                        "Response body had an unexpected shape.", status, quota);
                }

                // Clone() detaches the element so it survives disposal of the document.
                return (root.Clone(), quota!);
            }
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
#if NET8_0_OR_GREATER
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        }
        catch (Exception ex)
        {
            throw new TransportException("Failed to read the response body.", (int)response.StatusCode, innerException: ex);
        }
    }

    private static DemografixException MapError(int status, JsonElement root, Quota? quota)
    {
        var message = ExtractErrorMessage(root, status);
        return status switch
        {
            401 => new AuthException(message, status, quota),
            402 => new SubscriptionException(message, status, quota),
            422 => new ValidationException(message, status, quota),
            429 => new RateLimitException(message, status, quota),
            _ => new DemografixException(message, status, quota),
        };
    }

    private static string ExtractErrorMessage(JsonElement root, int status)
    {
        // The body is already known to be valid JSON. Pull the "error" string through when present.
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.String)
        {
            return error.GetString() ?? $"HTTP {status}";
        }
        return $"HTTP {status}";
    }

    // ---- header parsing ----

    private static Quota? ParseQuota(HttpResponseMessage response)
    {
        var limit = ReadHeaderInt(response, "x-rate-limit-limit");
        var remaining = ReadHeaderInt(response, "x-rate-limit-remaining");
        var reset = ReadHeaderInt(response, "x-rate-limit-reset");

        if (limit is null || remaining is null || reset is null)
        {
            return null;
        }
        return new Quota(limit.Value, remaining.Value, reset.Value);
    }

    private static int? ReadHeaderInt(HttpResponseMessage response, string name)
    {
        // HttpHeaders lookups are case-insensitive; check both the response and content collections.
        if (response.Headers.TryGetValues(name, out var values) ||
            (response.Content?.Headers.TryGetValues(name, out values) ?? false))
        {
            foreach (var value in values)
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }
        return null;
    }

    // ---- body parsing ----

    private static GenderizePrediction ParseGenderize(JsonElement element)
    {
        var name = GetString(element, "name") ?? string.Empty;
        string? gender = GetString(element, "gender");
        double probability = GetDouble(element, "probability");
        int count = GetInt(element, "count");
        string? countryId = GetString(element, "country_id");
        return new GenderizePrediction(name, gender, probability, count, countryId);
    }

    private static AgifyPrediction ParseAgify(JsonElement element)
    {
        var name = GetString(element, "name") ?? string.Empty;
        int? age = GetNullableInt(element, "age");
        int count = GetInt(element, "count");
        string? countryId = GetString(element, "country_id");
        return new AgifyPrediction(name, age, count, countryId);
    }

    private static NationalizePrediction ParseNationalize(JsonElement element)
    {
        var name = GetString(element, "name") ?? string.Empty;
        int count = GetInt(element, "count");
        var countries = new List<NationalizeCountry>();
        if (element.TryGetProperty("country", out var country) && country.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in country.EnumerateArray())
            {
                var id = GetString(candidate, "country_id") ?? string.Empty;
                var probability = GetDouble(candidate, "probability");
                countries.Add(new NationalizeCountry(id, probability));
            }
        }
        return new NationalizePrediction(name, countries, count);
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
        return null;
    }

    private static double GetDouble(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }
        return 0.0;
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetInt32();
        }
        return 0;
    }

    private static int? GetNullableInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetInt32();
        }
        return null;
    }

    /// <summary>
    /// Disposes the underlying <see cref="HttpClient"/> owned by this instance. When a test injects a handler,
    /// the handler is left for the test to dispose.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
