namespace Modules.Ingestion.Domain.Tests;

// See Atlas.Kernel.Tests/BusinessRuleAttribute.cs for why each test project defines its own copy.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BusinessRuleAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
