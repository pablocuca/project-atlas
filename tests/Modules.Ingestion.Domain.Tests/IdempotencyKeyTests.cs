using Atlas.Modules.Ingestion.Domain;
using FsCheck.Xunit;

namespace Modules.Ingestion.Domain.Tests;

public class IdempotencyKeyTests
{
    // docs/03-architecture/05-ingestion-and-integration.md §4: computed from the raw record, so the
    // same source data always produces the same key.
    [Property]
    public bool Same_inputs_always_produce_the_same_key(string sourceId, string rawRecord)
    {
        var first = IdempotencyKey.Compute(sourceId, rawRecord);
        var second = IdempotencyKey.Compute(sourceId, rawRecord);
        return first == second;
    }

    // The separator character must prevent (sourceId="ab", record="c") from colliding with
    // (sourceId="a", record="bc") — a plain concatenation without a separator would not guarantee this.
    [Fact]
    public void Different_source_record_splits_never_collide()
    {
        var first = IdempotencyKey.Compute("ab", "c");
        var second = IdempotencyKey.Compute("a", "bc");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Different_records_produce_different_keys()
    {
        var first = IdempotencyKey.Compute("bank", "2026-01-05,Salary,8000.00");
        var second = IdempotencyKey.Compute("bank", "2026-01-05,Salary,8000.01");

        Assert.NotEqual(first, second);
    }
}
