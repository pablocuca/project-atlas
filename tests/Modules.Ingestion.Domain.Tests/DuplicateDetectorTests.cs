using Atlas.Modules.Ingestion.Domain;

namespace Modules.Ingestion.Domain.Tests;

public class DuplicateDetectorTests
{
    private static readonly DateTimeOffset Day = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Same_amount_close_date_and_similar_description_is_flagged()
    {
        var existing = new[] { new ExistingEntrySummary(Guid.NewGuid(), Day, "JOAO S PIX RECEBIDO", 120_000) };

        var candidates = DuplicateDetector.FindCandidates(
            Day.AddDays(1), "Joao", 120_000, Guid.NewGuid(), existing);

        Assert.Single(candidates);
    }

    [Fact]
    public void Different_amount_is_never_flagged_even_with_identical_description()
    {
        var existing = new[] { new ExistingEntrySummary(Guid.NewGuid(), Day, "Joao", 120_000) };

        var candidates = DuplicateDetector.FindCandidates(Day, "Joao", 120_001, Guid.NewGuid(), existing);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Outside_the_two_day_window_is_never_flagged()
    {
        var existing = new[] { new ExistingEntrySummary(Guid.NewGuid(), Day, "Joao", 120_000) };

        var candidates = DuplicateDetector.FindCandidates(Day.AddDays(3), "Joao", 120_000, Guid.NewGuid(), existing);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Dissimilar_description_is_never_flagged_even_with_matching_amount_and_date()
    {
        var existing = new[] { new ExistingEntrySummary(Guid.NewGuid(), Day, "Netflix subscription", 120_000) };

        var candidates = DuplicateDetector.FindCandidates(Day, "Uber ride", 120_000, Guid.NewGuid(), existing);

        Assert.Empty(candidates);
    }

    [Fact]
    public void An_entry_is_never_a_candidate_against_itself()
    {
        var selfId = Guid.NewGuid();
        var existing = new[] { new ExistingEntrySummary(selfId, Day, "Joao", 120_000) };

        var candidates = DuplicateDetector.FindCandidates(Day, "Joao", 120_000, selfId, existing);

        Assert.Empty(candidates);
    }
}
