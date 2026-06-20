using System;

namespace Demografix;

/// <summary>
/// Base type for every error raised by the SDK. Carries the HTTP status when known and the response quota
/// when the rate-limit headers were present.
/// </summary>
public class DemografixException : Exception
{
    /// <summary>The HTTP status code, or null for transport-level failures.</summary>
    public int? Status { get; }

    /// <summary>The response quota when the rate-limit headers were present, otherwise null.</summary>
    public Quota? Quota { get; }

    /// <summary>Creates a new <see cref="DemografixException"/>.</summary>
    /// <param name="message">The error message, passed through from the API where available.</param>
    /// <param name="status">The HTTP status code, or null for transport-level failures.</param>
    /// <param name="quota">The response quota when the rate-limit headers were present, otherwise null.</param>
    /// <param name="innerException">The underlying exception, when one caused this failure.</param>
    public DemografixException(string message, int? status = null, Quota? quota = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        Quota = quota;
    }
}

/// <summary>Raised on a 401 response (invalid or missing API key).</summary>
public sealed class AuthException : DemografixException
{
    /// <summary>Creates a new <see cref="AuthException"/>.</summary>
    /// <param name="message">The error message, passed through from the API.</param>
    /// <param name="status">The HTTP status code; 401 by default.</param>
    /// <param name="quota">The response quota when the rate-limit headers were present.</param>
    public AuthException(string message, int? status = 401, Quota? quota = null)
        : base(message, status, quota)
    {
    }
}

/// <summary>Raised on a 402 response (expired freebie or inactive subscription).</summary>
public sealed class SubscriptionException : DemografixException
{
    /// <summary>Creates a new <see cref="SubscriptionException"/>.</summary>
    /// <param name="message">The error message, passed through from the API.</param>
    /// <param name="status">The HTTP status code; 402 by default.</param>
    /// <param name="quota">The response quota when the rate-limit headers were present.</param>
    public SubscriptionException(string message, int? status = 402, Quota? quota = null)
        : base(message, status, quota)
    {
    }
}

/// <summary>
/// Raised on a 422 response, and client-side before any HTTP call when a batch exceeds ten names.
/// </summary>
public sealed class ValidationException : DemografixException
{
    /// <summary>Creates a new <see cref="ValidationException"/>.</summary>
    /// <param name="message">The error message; passed through from the API, or a client-side message when a batch exceeds ten names.</param>
    /// <param name="status">The HTTP status code; 422 by default, null when raised client-side.</param>
    /// <param name="quota">The response quota when the rate-limit headers were present.</param>
    public ValidationException(string message, int? status = 422, Quota? quota = null)
        : base(message, status, quota)
    {
    }
}

/// <summary>Raised on a 429 response. The quota is always populated, so <c>Quota.Reset</c> tells you how long to wait.</summary>
public sealed class RateLimitException : DemografixException
{
    /// <summary>Creates a new <see cref="RateLimitException"/>.</summary>
    /// <param name="message">The error message, passed through from the API.</param>
    /// <param name="status">The HTTP status code; 429 by default.</param>
    /// <param name="quota">The response quota; always populated for a rate-limit response.</param>
    public RateLimitException(string message, int? status = 429, Quota? quota = null)
        : base(message, status, quota)
    {
    }
}

/// <summary>Raised on a network failure, timeout, or a non-JSON body. Status and quota may be absent.</summary>
public sealed class TransportException : DemografixException
{
    /// <summary>Creates a new <see cref="TransportException"/>.</summary>
    /// <param name="message">A description of the transport-level failure.</param>
    /// <param name="status">The HTTP status code when one was received, otherwise null.</param>
    /// <param name="quota">The response quota when the rate-limit headers were present, otherwise null.</param>
    /// <param name="innerException">The underlying network or parse exception, when one caused this failure.</param>
    public TransportException(string message, int? status = null, Quota? quota = null, Exception? innerException = null)
        : base(message, status, quota, innerException)
    {
    }
}
