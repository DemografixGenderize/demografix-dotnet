using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Demografix.Tests;

/// <summary>
/// A test-double <see cref="HttpMessageHandler"/> that returns a canned response and records the request it
/// received. No network is involved.
/// </summary>
internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly IReadOnlyDictionary<string, string> _headers;

    public HttpRequestMessage? LastRequest { get; private set; }
    public int CallCount { get; private set; }

    public FakeHandler(HttpStatusCode status, string body, IReadOnlyDictionary<string, string>? headers = null)
    {
        _status = status;
        _body = body;
        _headers = headers ?? DefaultHeaders;
    }

    public static IReadOnlyDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>
    {
        ["x-rate-limit-limit"] = "25000",
        ["x-rate-limit-remaining"] = "24987",
        ["x-rate-limit-reset"] = "1314000",
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Honor cancellation the way a real transport would, so the client's token plumbing is exercised.
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastRequest = request;

        var response = new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body),
            RequestMessage = request,
        };

        foreach (var pair in _headers)
        {
            // Add to the response collection; HttpClient lookups are case-insensitive.
            response.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }

        return Task.FromResult(response);
    }
}

/// <summary>
/// A handler that throws before producing any response, simulating a thrown-but-never-called transport.
/// Used to prove the client-side batch guard fires with no HTTP call.
/// </summary>
internal sealed class ThrowingHandler : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        throw new InvalidOperationException("No HTTP call should have been made.");
    }
}

/// <summary>
/// Builds a client through the internal test-seam constructor
/// <c>DemografixClient(string?, TimeSpan?, HttpMessageHandler?)</c>, reachable here because the source
/// assembly grants InternalsVisibleTo("Demografix.Tests"). Keeps construction in one place.
/// </summary>
internal static class TestClient
{
    public static DemografixClient Create(HttpMessageHandler handler, string apiKey = "test-key")
    {
        return new DemografixClient(apiKey, timeout: TimeSpan.FromSeconds(10), handler: handler);
    }
}
