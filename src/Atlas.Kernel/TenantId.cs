namespace Atlas.Kernel;

// INV-003: every aggregate root carries a TenantId.
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
