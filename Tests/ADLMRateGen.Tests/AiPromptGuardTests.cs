using ADLMRateGen.ViewModel.CustomRate;
using Xunit;

namespace ADLMRateGen.Tests;

/// <summary>
/// The Build with AI box only accepts a prompt that asks for a rate build-up in
/// so many words. Every request bills against the account's AI allowance, and the
/// service answers whatever it is given, so a prompt that is not a work item
/// spends a request producing something RateGen cannot use.
///
/// These tests pin the rule as it stands — a stem match on "rate" and "build",
/// case-insensitive — including the two cases where a keyword check and the
/// intent behind it disagree.
/// </summary>
public class AiPromptGuardTests
{
    [Theory]
    [InlineData("Build a rate for 225mm hollow sandcrete blockwork in cement-sand mortar (1:6)")]
    [InlineData("build me a rate for 150mm mass concrete bed")]
    [InlineData("BUILD A RATE FOR PLASTERING")]
    [InlineData("I need a rate built for 12mm cement screed")]
    [InlineData("Building a rate for roof carpentry, 50x100mm rafters")]
    [InlineData("rates: build up for excavation in firm soil")]
    public void AcceptsAPromptAskingForARateBuildUp(string prompt) =>
        Assert.True(CustomRateEntryViewModel.MentionsRateAndBuild(prompt));

    [Theory]
    [InlineData("225mm hollow sandcrete blockwork in cement-sand mortar (1:6)")]
    [InlineData("what is the capital of France")]
    [InlineData("give me a price for cement")]
    [InlineData("build me a house")]
    [InlineData("what rate should I use for plastering")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsAPromptMissingEitherWord(string? prompt) =>
        Assert.False(CustomRateEntryViewModel.MentionsRateAndBuild(prompt));

    [Fact]
    public void TheStemMatches_SoInflectionsCount()
    {
        // "rated"/"builds" are not separate rules — the check is a substring on
        // the stem, which is what lets "building" and "rates" through above.
        Assert.True(CustomRateEntryViewModel.MentionsRateAndBuild("builds rates"));
        Assert.True(CustomRateEntryViewModel.MentionsRateAndBuild("rebuild the rate"));
    }

    [Fact]
    public void AKeywordCheckIsNotComprehension()
    {
        // Documented, not desired. A keyword guard cannot tell a work item from
        // anything else that happens to contain both words, and it turns away a
        // bare work item — which is exactly what a QS types unprompted.
        //
        // If these two lines start costing users more than the guard saves,
        // the fix is to classify the prompt rather than scan it for words.
        Assert.True(CustomRateEntryViewModel.MentionsRateAndBuild("build a rate for a birthday cake"));
        Assert.False(CustomRateEntryViewModel.MentionsRateAndBuild("150mm thick sandcrete blockwork"));
    }
}
