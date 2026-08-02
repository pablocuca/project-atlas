using Atlas.Modules.Ingestion.Domain;

namespace Modules.Ingestion.Domain.Tests;

public class StringSimilarityTests
{
    [Fact]
    public void Identical_strings_are_perfectly_similar()
    {
        Assert.Equal(1.0, StringSimilarity.Compute("JOAO SILVA", "JOAO SILVA"));
    }

    [Fact]
    public void Case_and_surrounding_whitespace_do_not_affect_similarity()
    {
        Assert.Equal(1.0, StringSimilarity.Compute("  Joao Silva  ", "JOAO SILVA"));
    }

    [Fact]
    public void The_docs_example_pair_clears_the_085_threshold()
    {
        // docs/03-architecture/05-ingestion-and-integration.md §4's own example: "Joao" (manual) vs
        // "JOAO S" (bank feed) for the same PIX transfer.
        var similarity = StringSimilarity.Compute("Joao", "JOAO S");
        Assert.True(similarity >= 0.85, $"Expected >= 0.85, got {similarity}");
    }

    [Fact]
    public void Unrelated_strings_fall_well_below_the_threshold()
    {
        var similarity = StringSimilarity.Compute("Salary", "Grocery Store XYZ");
        Assert.True(similarity < 0.85, $"Expected < 0.85, got {similarity}");
    }

    [Fact]
    public void Empty_strings_are_perfectly_similar_to_each_other()
    {
        Assert.Equal(1.0, StringSimilarity.Compute("", ""));
    }
}
