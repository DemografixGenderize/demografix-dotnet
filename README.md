# Demografix for C#

Run demographic analysis over names — predicted gender, age, and nationality — from one client. The official
C# package covers [genderize.io](https://genderize.io), [agify.io](https://agify.io), and
[nationalize.io](https://nationalize.io).

## Install

```sh
dotnet add package Demografix
```

The package targets `netstandard2.0`, `net8.0`, and `net10.0`.

## Quickstart

Construct a client, run a batch over a list of names, read the predictions in aggregate, and read the
remaining quota.

```csharp
using System.Linq;
using Demografix;

using var client = new DemografixClient(apiKey: "YOUR_API_KEY");

var names = new[] { "michael", "matthew", "jane" };
var ages = await client.AgifyBatchAsync(names);

// Aggregate the predictions; do not label a person.
var distribution = ages.Results
    .Where(r => r.Age.HasValue)
    .GroupBy(r => r.Age!.Value / 10 * 10)
    .ToDictionary(g => $"{g.Key}s", g => g.Count());

Console.WriteLine(ages.Quota.Remaining); // 24987
```

The constructor takes a required `apiKey` and an optional `timeout` (default ten seconds). The service hosts
and the User-Agent are fixed constants, not options.

An API key is required. Creating one is free and includes 2,500 requests per month. Generate a key in your
dashboard at [genderize.io](https://genderize.io), [agify.io](https://agify.io), or
[nationalize.io](https://nationalize.io). One key works across all three services.

## genderize

Predict gender across a list and summarize the split.

```csharp
using var client = new DemografixClient("YOUR_API_KEY");

var one = await client.GenderizeAsync("peter");
// one.Gender -> "male", one.Probability -> 1.0

var batch = await client.GenderizeBatchAsync(new[] { "peter", "lois", "jane" });
var split = batch.Results
    .GroupBy(r => r.Gender ?? "unknown")
    .ToDictionary(g => g.Key, g => g.Count());
```

`GenderizePrediction` exposes `Name`, `Gender` (`"male"`, `"female"`, or `null`), `Probability`, `Count`, and
`CountryId`.

## agify

Predict age across a list and build a distribution.

```csharp
using var client = new DemografixClient("YOUR_API_KEY");

var one = await client.AgifyAsync("michael");
// one.Age -> 57

var batch = await client.AgifyBatchAsync(new[] { "michael", "matthew", "jane" });
var byDecade = batch.Results
    .Where(r => r.Age.HasValue)
    .GroupBy(r => r.Age!.Value / 10 * 10)
    .ToDictionary(g => $"{g.Key}s", g => g.Count());
```

`AgifyPrediction` exposes `Name`, `Age` (`int` or `null`), `Count`, and `CountryId`.

## nationalize

Predict nationality across a list and tally the mix.

```csharp
using var client = new DemografixClient("YOUR_API_KEY");

var one = await client.NationalizeAsync("nguyen");
// one.Country[0].CountryId -> "VN"

var batch = await client.NationalizeBatchAsync(new[] { "nguyen", "smith", "garcia" });
var mix = batch.Results
    .Where(r => r.Country.Count > 0)
    .GroupBy(r => r.Country[0].CountryId)
    .ToDictionary(g => g.Key, g => g.Count());
```

`NationalizePrediction` exposes `Name`, `Country` (up to five `NationalizeCountry` candidates in descending
probability), and `Count`. `NationalizeCountry` exposes `CountryId` and `Probability`.

## country_id

`GenderizeAsync`, `GenderizeBatchAsync`, `AgifyAsync`, and `AgifyBatchAsync` accept an optional `countryId`
(ISO 3166-1 alpha-2) that scopes the prediction to one country. The server echoes it back uppercase on each
prediction. `nationalize` does not take this parameter. Pass it on a batch to scope a whole list and summarize
the result in aggregate.

```csharp
var names = new[] { "kim", "andrea", "jan" };
var batch = await client.GenderizeBatchAsync(names, countryId: "us");

var split = batch.Results
    .GroupBy(r => r.Gender ?? "unknown")
    .ToDictionary(g => g.Key, g => g.Count());
// batch.Results[0].CountryId -> "US" on every row
```

## Quota

Every result and every raised error carries a `Quota` with three fields read from the rate-limit response
headers. Read it off the returned value or the caught error; it is never cached on the client.

| Field | Meaning |
|---|---|
| `Limit` | names allowed in the current window |
| `Remaining` | names left in the current window |
| `Reset` | seconds until the window resets |

## Errors

Non-2xx responses throw a typed exception. Transport failures throw `TransportException`. Every exception
extends `DemografixException` and carries `Status`, `Message`, and `Quota` (when the headers were present).

| Status | Exception |
|---|---|
| 401 | `AuthException` |
| 402 | `SubscriptionException` |
| 422 | `ValidationException` |
| 429 | `RateLimitException` |
| other non-2xx | `DemografixException` |
| network / timeout / non-JSON | `TransportException` |

A batch of more than ten names throws `ValidationException` before any HTTP call. A `RateLimitException`
always carries `Quota`, so `Quota.Reset` tells you how long to wait.

```csharp
try
{
    var batch = await client.GenderizeBatchAsync(names);
}
catch (RateLimitException ex)
{
    await Task.Delay(TimeSpan.FromSeconds(ex.Quota!.Reset));
    // retry
}
```

## Methods

| Method | Returns | country_id |
|---|---|---|
| `GenderizeAsync(name, countryId?)` | `GenderizeResult` | yes |
| `GenderizeBatchAsync(names, countryId?)` | `Batch<GenderizePrediction>` | yes |
| `AgifyAsync(name, countryId?)` | `AgifyResult` | yes |
| `AgifyBatchAsync(names, countryId?)` | `Batch<AgifyPrediction>` | yes |
| `NationalizeAsync(name)` | `NationalizeResult` | no |
| `NationalizeBatchAsync(names)` | `Batch<NationalizePrediction>` | no |

A single result exposes the prediction fields plus a `Quota`. A batch result exposes `Results` (the per-name
predictions) plus one `Quota` for the response.

## Reference

Full API reference: <https://genderize.io/documentation/api>. One API key works across all three services.
