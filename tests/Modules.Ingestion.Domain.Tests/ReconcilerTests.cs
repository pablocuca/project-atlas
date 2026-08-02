using Atlas.Kernel;
using Atlas.Modules.Ingestion.Domain;
using FsCheck.Xunit;

namespace Modules.Ingestion.Domain.Tests;

public class ReconcilerTests
{
    // US-012 (docs/01-product/10-user-stories.md): exact match reconciles.
    [Fact]
    [BusinessRule("BR-108")]
    public void Exact_match_is_reconciled()
    {
        var reported = Money.FromMinorUnits(1_432_891, Commodity.Brl); // R$14.328,91
        var ledger = Money.FromMinorUnits(1_432_891, Commodity.Brl);

        var outcome = Reconciler.Reconcile(reported, ledger);

        Assert.True(outcome.IsReconciled);
        Assert.Equal(0, outcome.DiscrepancyMinorUnits);
    }

    // US-012: R$14.328,91 reported vs R$14.290,00 ledger -> R$38,91 discrepancy, well outside
    // tolerance, never a silent fix.
    [Fact]
    [BusinessRule("BR-108")]
    public void The_docs_drift_scenario_is_not_reconciled()
    {
        var reported = Money.FromMinorUnits(1_432_891, Commodity.Brl);
        var ledger = Money.FromMinorUnits(1_429_000, Commodity.Brl);

        var outcome = Reconciler.Reconcile(reported, ledger);

        Assert.False(outcome.IsReconciled);
        Assert.Equal(3_891, outcome.DiscrepancyMinorUnits); // R$38,91
    }

    // The M0/M1 exit gate's own tolerance line ("reconciled to <= R$1,00"), adopted as BR-108's
    // formal tolerance in docs/decisions/0008.
    [Fact]
    public void Exactly_one_real_off_the_tolerance_boundary_still_reconciles()
    {
        var reported = Money.FromMinorUnits(100_000, Commodity.Brl);
        var ledger = Money.FromMinorUnits(100_100, Commodity.Brl); // R$1,00 off

        var outcome = Reconciler.Reconcile(reported, ledger);

        Assert.True(outcome.IsReconciled);
    }

    [Fact]
    public void One_centavo_past_the_tolerance_boundary_does_not_reconcile()
    {
        var reported = Money.FromMinorUnits(100_000, Commodity.Brl);
        var ledger = Money.FromMinorUnits(100_101, Commodity.Brl); // R$1,01 off

        var outcome = Reconciler.Reconcile(reported, ledger);

        Assert.False(outcome.IsReconciled);
    }

    [Fact]
    public void Different_commodities_cannot_be_reconciled_against_each_other()
    {
        var reported = Money.FromMinorUnits(100_000, Commodity.Brl);
        var ledger = Money.FromMinorUnits(100_000, Commodity.Usd);

        Assert.Throws<InvalidOperationException>(() => Reconciler.Reconcile(reported, ledger));
    }

    [Property]
    public bool Discrepancy_is_always_symmetric(int reportedCents, int ledgerCents)
    {
        var forward = Reconciler.Reconcile(Money.FromMinorUnits(reportedCents, Commodity.Brl), Money.FromMinorUnits(ledgerCents, Commodity.Brl));
        var backward = Reconciler.Reconcile(Money.FromMinorUnits(ledgerCents, Commodity.Brl), Money.FromMinorUnits(reportedCents, Commodity.Brl));

        return forward.DiscrepancyMinorUnits == backward.DiscrepancyMinorUnits && forward.IsReconciled == backward.IsReconciled;
    }
}
