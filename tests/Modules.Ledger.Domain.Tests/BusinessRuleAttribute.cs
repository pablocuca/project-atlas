namespace Modules.Ledger.Domain.Tests;

// docs/05-engineering/03-testing-strategy.md §7: the rule-coverage gate scans test assemblies for
// this attribute and fails the build on any BR- identifier with no test.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BusinessRuleAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
