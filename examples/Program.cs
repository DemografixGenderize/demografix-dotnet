using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Demografix;

// Summarize the demographic mix of a list of names. The deliverable is the aggregate, not a per-name label.

// An API key is required; the same key works across all three services.
var apiKey = Environment.GetEnvironmentVariable("DEMOGRAFIX_API_KEY")
    ?? throw new InvalidOperationException("Set DEMOGRAFIX_API_KEY to your Demografix API key.");
using var client = new DemografixClient(apiKey);

var names = new[] { "michael", "matthew", "jane", "nguyen", "lois", "peter" };

// Gender split across the list.
var genders = await client.GenderizeBatchAsync(names);
var genderSplit = genders.Results
    .GroupBy(p => p.Gender ?? "unknown")
    .ToDictionary(g => g.Key, g => g.Count());

Console.WriteLine("Gender split:");
foreach (var pair in genderSplit.OrderByDescending(p => p.Value))
{
    Console.WriteLine($"  {pair.Key}: {pair.Value}");
}

// Age distribution across the list, bucketed by decade.
var ages = await client.AgifyBatchAsync(names);
var ageBuckets = ages.Results
    .Where(p => p.Age.HasValue)
    .GroupBy(p => $"{p.Age!.Value / 10 * 10}s")
    .ToDictionary(g => g.Key, g => g.Count());

Console.WriteLine("Age distribution:");
foreach (var pair in ageBuckets.OrderBy(p => p.Key))
{
    Console.WriteLine($"  {pair.Key}: {pair.Value}");
}

// Nationality mix: top country per name, tallied across the list.
var nationalities = await client.NationalizeBatchAsync(names);
var countryMix = new Dictionary<string, int>();
foreach (var prediction in nationalities.Results)
{
    if (prediction.Country.Count == 0)
    {
        continue;
    }
    var top = prediction.Country[0].CountryId;
    countryMix[top] = countryMix.GetValueOrDefault(top) + 1;
}

Console.WriteLine("Nationality mix (top country per name):");
foreach (var pair in countryMix.OrderByDescending(p => p.Value))
{
    Console.WriteLine($"  {pair.Key}: {pair.Value}");
}

Console.WriteLine($"Quota remaining: {nationalities.Quota.Remaining}");
