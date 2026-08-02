namespace Atlas.Kernel.Tests;

// docs/05-engineering/03-testing-strategy.md §7: the rule-coverage gate scans test assemblies for
// this attribute and fails the build on any BR- identifier with no test. Each test project defines
// its own copy rather than sharing one type across assemblies — Atlas.ArchitectureTests' rule-
// coverage gate matches by attribute *name*, not by binding to a single shared type (Decision 0013).
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BusinessRuleAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
