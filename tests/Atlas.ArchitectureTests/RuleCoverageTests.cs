using System.Reflection;
using System.Text.RegularExpressions;

namespace Atlas.ArchitectureTests;

// docs/05-engineering/03-testing-strategy.md §7 / TD-002. Scoped to BR- identifiers actually cited
// in src/ — not the full ~130-identifier catalog in business-rules.md, most of which belongs to
// engines (Forecast, Taxation, Advisory, ...) that don't exist yet. Requiring every eventual BR- to
// have a test today would never be green until the whole decade-scale system is built; requiring
// every BR- the CURRENT code claims to implement to have one is the gate that's actually meaningful
// right now (Decision 0013).
public partial class RuleCoverageTests
{
    // One assembly reference per test project that currently cites a BR- identifier — see the
    // ProjectReference comment in Atlas.ArchitectureTests.csproj for why these are compile-time
    // references rather than runtime-loaded DLLs.
    private static readonly Assembly[] TestAssemblies =
    [
        typeof(Atlas.Kernel.Tests.MoneyTests).Assembly,
        typeof(global::Modules.Ledger.Domain.Tests.AccountTests).Assembly,
        typeof(global::Modules.Ingestion.Domain.Tests.ReconcilerTests).Assembly,
    ];

    [Fact]
    public void Every_BR_cited_in_src_has_at_least_one_attributed_test()
    {
        var repoRoot = FindRepoRoot();
        var citedInSrc = ScanForBusinessRuleCitations(Path.Combine(repoRoot, "src"));
        var covered = ScanTestAssembliesForBusinessRuleAttributes();

        var missing = citedInSrc.Except(covered.Keys).Order().ToArray();
        Assert.True(
            missing.Length == 0,
            "These BR- identifiers are cited in src/ but have no [BusinessRule(\"BR-nnn\")]-attributed " +
            $"test in any referenced test project: {string.Join(", ", missing)}");
    }

    // Catches a typo in a [BusinessRule("BR-nnn")] citation — an id that doesn't exist in the
    // canonical catalog can't mean what its author intended.
    [Fact]
    public void Every_attributed_BR_citation_exists_in_the_canonical_catalog()
    {
        var repoRoot = FindRepoRoot();
        var canonicalIds = ParseCanonicalBusinessRuleIds(repoRoot);
        var covered = ScanTestAssembliesForBusinessRuleAttributes();

        var unknown = covered.Keys.Except(canonicalIds).Order().ToArray();
        Assert.True(
            unknown.Length == 0,
            "These [BusinessRule] test citations don't match any ID in docs/02-domain/05-business-rules.md " +
            $"(typo?): {string.Join(", ", unknown)}");
    }

    // Testing Strategy §7 step 4: informational only, never fails the build — a second test for an
    // already-covered rule is a nice-to-have, not a requirement.
    [Fact]
    public void Reports_BR_identifiers_with_only_one_test()
    {
        var covered = ScanTestAssembliesForBusinessRuleAttributes();

        foreach (var id in covered.Where(kv => kv.Value == 1).Select(kv => kv.Key).Order())
            Console.WriteLine($"[rule-coverage] {id} has exactly one attributed test.");
    }

    private static Dictionary<string, int> ScanTestAssembliesForBusinessRuleAttributes()
    {
        var counts = new Dictionary<string, int>();

        foreach (var assembly in TestAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (var attribute in method.GetCustomAttributesData())
                    {
                        // Matched by name, not by binding to a single shared type — each test
                        // project defines its own copy of BusinessRuleAttribute (see that type's
                        // own comment for why).
                        if (attribute.AttributeType.Name != "BusinessRuleAttribute")
                            continue;

                        var id = (string)attribute.ConstructorArguments[0].Value!;
                        counts[id] = counts.GetValueOrDefault(id) + 1;
                    }
                }
            }
        }

        return counts;
    }

    private static HashSet<string> ScanForBusinessRuleCitations(string directory)
    {
        var ids = new HashSet<string>();

        foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in BusinessRuleIdPattern().Matches(File.ReadAllText(file)))
                ids.Add(match.Value);
        }

        return ids;
    }

    private static HashSet<string> ParseCanonicalBusinessRuleIds(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "docs", "02-domain", "05-business-rules.md");
        var content = File.ReadAllText(path);

        return [.. BusinessRuleIdPattern().Matches(content).Select(m => m.Value)];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "atlas.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException($"Could not locate repo root (atlas.sln) from {AppContext.BaseDirectory}.");
    }

    // BR-xxx (three digits, e.g. BR-108) or BR-Xnn (a letter band, e.g. BR-B01, BR-A00).
    [GeneratedRegex(@"\bBR-[0-9A-Za-z]{2,3}\b")]
    private static partial Regex BusinessRuleIdPattern();
}
