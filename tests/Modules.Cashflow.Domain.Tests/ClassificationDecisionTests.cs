using Atlas.Kernel;
using Atlas.Modules.Cashflow.Application;
using Atlas.Modules.Cashflow.Domain;

namespace Modules.Cashflow.Domain.Tests;

public class ClassificationDecisionTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    // US-013: an unclassified category has no history, so Current() is null — "unclassified" isn't
    // a stored state, it's simply the absence of any decision.
    [Fact]
    public void An_unclassified_category_has_no_current_decision()
    {
        IReadOnlyList<ClassificationDecision> history = [];

        Assert.Null(history.Current());
    }

    // US-013's reclassification scenario: reclassifying "Plano de saúde" from Discretionary to
    // Essential must not erase the original decision — INV-060's audit trail requires both to
    // remain, with Current() reflecting only the most recent one.
    [Fact]
    public void Reclassifying_keeps_the_prior_decision_in_history_and_updates_current()
    {
        var tenantId = TenantId.New();
        var categoryAccountId = Guid.NewGuid();

        var original = ClassificationDecision.Create(
            tenantId, categoryAccountId, Classification.Discretionary, "initial guess", Day1);
        var reclassification = ClassificationDecision.Create(
            tenantId, categoryAccountId, Classification.Essential, "actually a health plan premium", Day1.AddDays(30));

        IReadOnlyList<ClassificationDecision> history = [original, reclassification];

        Assert.Equal(2, history.Count);
        Assert.Equal(Classification.Essential, history.Current()!.Classification);
        Assert.Contains(history, d => d.Classification == Classification.Discretionary);
    }

    [Fact]
    public void Current_picks_the_most_recent_decision_regardless_of_list_order()
    {
        var tenantId = TenantId.New();
        var categoryAccountId = Guid.NewGuid();

        var mostRecentByTime = ClassificationDecision.Create(
            tenantId, categoryAccountId, Classification.Discretionary, null, Day1.AddDays(10));
        var earlierByTime = ClassificationDecision.Create(
            tenantId, categoryAccountId, Classification.CommittedDiscretionary, null, Day1.AddDays(1));

        // mostRecentByTime is listed first but earlierByTime was decided first chronologically —
        // Current() must go by DecidedAt, not list position.
        IReadOnlyList<ClassificationDecision> history = [mostRecentByTime, earlierByTime];

        Assert.Equal(Classification.Discretionary, history.Current()!.Classification);
    }
}
