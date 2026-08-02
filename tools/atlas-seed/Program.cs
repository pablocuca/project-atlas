using Atlas.Seed;

var options = CliOptions.Parse(args);

Console.WriteLine($"atlas-seed: years={options.Years} seed={options.Seed} host={options.Host}");

var life = SyntheticLifeGenerator.Generate(options.Seed, options.Years, options.AsOf);
Console.WriteLine($"Generated {life.Accounts.Count} accounts and {life.Entries.Count} entries " +
                   $"({life.Entries.Count(e => e.Correction is not null)} with a correction).");

using var httpClient = new HttpClient { BaseAddress = new Uri(options.Host) };
var client = new AtlasHostClient(httpClient);

var accountIdsByCode = new Dictionary<string, Guid>();
foreach (var account in life.Accounts)
{
    var id = await client.OpenAccountAsync(life.TenantId, account, CancellationToken.None);
    accountIdsByCode[account.Code] = id;
}
Console.WriteLine($"Opened {accountIdsByCode.Count} accounts.");

var posted = 0;
var duplicates = 0;
var corrected = 0;
var correctionDuplicates = 0;

foreach (var entry in life.Entries)
{
    var entryId = await client.PostEntryAsync(life.TenantId, accountIdsByCode, entry, CancellationToken.None);
    if (entryId is null)
    {
        duplicates++;
        continue;
    }

    posted++;

    if (entry.Correction is { } correction)
    {
        var applied = await client.CorrectEntryAsync(life.TenantId, entryId.Value, accountIdsByCode, correction, CancellationToken.None);
        if (applied)
            corrected++;
        else
            correctionDuplicates++;
    }
}

Console.WriteLine($"Posted {posted} entries ({duplicates} already present — BR-103 duplicate rejection, expected on a rerun).");
Console.WriteLine($"Applied {corrected} corrections ({correctionDuplicates} already present).");

if (life.Entries.Count < 1000)
{
    Console.Error.WriteLine($"Generated only {life.Entries.Count} entries; the M0 exit gate wants at least 1,000. " +
                             "Increase --years.");
    return 1;
}

var verifier = new Verifier(client);
var verification = await verifier.VerifyAsync(life.TenantId, life, accountIdsByCode, randomSampleSize: 100, new Random(options.Seed), CancellationToken.None);

Console.WriteLine($"Verified {verification.ChecksPerformed} balance queries at independently computed bitemporal coordinates.");

if (!verification.Success)
{
    Console.Error.WriteLine($"{verification.Mismatches.Count} verification mismatches:");
    foreach (var mismatch in verification.Mismatches)
        Console.Error.WriteLine($"  {mismatch}");
    return 1;
}

Console.WriteLine("OK — all balance queries matched the independently computed expectation.");
return 0;

internal sealed record CliOptions(int Years, int Seed, string Host, DateTimeOffset? AsOf)
{
    public static CliOptions Parse(string[] args)
    {
        var years = 10;
        var seed = 42;
        var host = "http://localhost:5299";
        DateTimeOffset? asOf = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--years" when i + 1 < args.Length:
                    years = int.Parse(args[++i]);
                    break;
                case "--seed" when i + 1 < args.Length:
                    seed = int.Parse(args[++i]);
                    break;
                case "--host" when i + 1 < args.Length:
                    host = args[++i];
                    break;
                case "--as-of" when i + 1 < args.Length:
                    asOf = DateTimeOffset.Parse(args[++i]);
                    break;
            }
        }

        return new CliOptions(years, seed, host, asOf);
    }
}
