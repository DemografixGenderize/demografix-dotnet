using System.Collections.Generic;

namespace Demografix;

/// <summary>
/// Rate-limit quota parsed from the response headers, read off a returned value or a raised error.
/// </summary>
public sealed record Quota(int Limit, int Remaining, int Reset);

/// <summary>
/// A genderize prediction for one name. <see cref="CountryId"/> is populated only when the request sent one.
/// </summary>
public sealed record GenderizePrediction(
    string Name,
    string? Gender,
    double Probability,
    int Count,
    string? CountryId);

/// <summary>
/// An agify prediction for one name. <see cref="CountryId"/> is populated only when the request sent one.
/// </summary>
public sealed record AgifyPrediction(
    string Name,
    int? Age,
    int Count,
    string? CountryId);

/// <summary>
/// One nationality candidate inside a <see cref="NationalizePrediction"/>.
/// </summary>
public sealed record NationalizeCountry(string CountryId, double Probability);

/// <summary>
/// A nationalize prediction for one name, with up to five candidate countries in descending probability.
/// </summary>
public sealed record NationalizePrediction(
    string Name,
    IReadOnlyList<NationalizeCountry> Country,
    int Count);

/// <summary>
/// A single genderize result: the prediction fields plus the response quota.
/// </summary>
public sealed record GenderizeResult(
    string Name,
    string? Gender,
    double Probability,
    int Count,
    string? CountryId,
    Quota Quota);

/// <summary>
/// A single agify result: the prediction fields plus the response quota.
/// </summary>
public sealed record AgifyResult(
    string Name,
    int? Age,
    int Count,
    string? CountryId,
    Quota Quota);

/// <summary>
/// A single nationalize result: the prediction fields plus the response quota.
/// </summary>
public sealed record NationalizeResult(
    string Name,
    IReadOnlyList<NationalizeCountry> Country,
    int Count,
    Quota Quota);

/// <summary>
/// A batch result: the per-name predictions in input order plus one quota for the whole response.
/// </summary>
public sealed record Batch<T>(IReadOnlyList<T> Results, Quota Quota);
