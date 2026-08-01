namespace Atlas.ArchitectureTests;

// docs/03-architecture/03-modular-monolith.md §2. As more modules arrive, MR-2..5, 7..10 gain
// meaning here too — with only Atlas.Kernel and one module's .Domain project to check, MR-1 and
// MR-6 are the rules this slice can actually exercise.
//
// These check *project references* directly via reflection (the exact thing MR-1/MR-6 constrain),
// rather than inferring intent from namespace usage — precise now, and cheap to keep precise as
// more modules and their inevitable BCL dependencies (System.Collections.Immutable, etc.) arrive.
// A namespace-graph tool (NetArchTest/ArchUnitNET) becomes worth its dependency once MR-2..10 need
// checking, which requires reasoning about which *module* a reference belongs to, not just whether
// it's "Atlas" or not.
public class ModularMonolithTests
{
    [Fact]
    public void MR_01_DomainProjectsDependOnlyOnKernel()
    {
        var domainAssembly = typeof(Atlas.Modules.Ledger.Domain.Account).Assembly;
        var atlasReferences = AtlasAssemblyReferences(domainAssembly);

        Assert.Equal(["Atlas.Kernel"], atlasReferences);
    }

    [Fact]
    public void MR_06_KernelReferencesNothing()
    {
        var kernelAssembly = typeof(Atlas.Kernel.Money).Assembly;
        var atlasReferences = AtlasAssemblyReferences(kernelAssembly);

        Assert.Empty(atlasReferences);
    }

    private static string[] AtlasAssemblyReferences(System.Reflection.Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null && name.StartsWith("Atlas.", StringComparison.Ordinal))
            .Select(name => name!)
            .Order()
            .ToArray();
}
