using ADLMRateGen.Services;
using Xunit;

namespace ADLMRateGen.Tests;

/// <summary>
/// Library lookups match on a normalised key, so a component named slightly
/// differently by the AI resolves to the row the user already has instead of
/// being harvested as a second copy of the same item.
///
/// The interesting half of this file is what must NOT collapse: normalising too
/// hard merges two genuinely different sizes, and a merged row misprices a rate
/// rather than merely tidying the library.
/// </summary>
public class LibraryNameMatchingTests
{
    [Theory]
    [InlineData("Cement (Portland 42.5R)", "Cement (Portland 42.5 R)")]
    [InlineData("Cement (Portland 42.5R)", "cement (portland 42.5r)")]
    [InlineData("3/4\"x4x8'(18x1200x2400mm)", "3/4\"x4x8' (18x1200x2400mm)")]
    [InlineData("Sharp sand", "Sharp  Sand")]
    [InlineData("Vibratory wheeled roller", "Vibratory-wheeled roller")]
    [InlineData("Mason, skilled", "Mason skilled")]
    [InlineData("Cement (Portland 42.5R) [AI]", "Cement (Portland 42.5R)")]
    [InlineData("Poker vibrator (plant)", "Poker vibrator")]
    public void NamesDifferingOnlyInStyleAreTheSameItem(string a, string b) =>
        Assert.True(RateLineLibrary.SameItem(a, b));

    [Theory]
    [InlineData("0.55mm roofing sheet", "0.55 mm roofing sheet", true)]
    [InlineData("1.2mm roofing sheet", "12mm roofing sheet", false)]
    [InlineData("Cement", "Coloured Cement", false)]
    [InlineData("Cement", "Loading and unloading cement", false)]
    [InlineData("Mortar (1:6)", "Mortar (1:3)", false)]
    [InlineData("Window 600 x 900mm", "Window 900 x 600mm", false)]
    [InlineData("Rebar 12mm", "Rebar 16mm", false)]
    public void SizesAndContentWordsStillSeparateItems(string a, string b, bool same) =>
        Assert.Equal(same, RateLineLibrary.SameItem(a, b));

    [Fact]
    public void TheDecimalPointIsLoadBearing()
    {
        // Stripping '.' would make these one key. 0.55mm, 1.2mm and 12mm are all
        // real roofing gauges, so collapsing them would price the wrong sheet —
        // which is why NormaliseKey removes brackets and spacing but not points.
        Assert.NotEqual(
            RateLineLibrary.NormaliseKey("1.2mm sheet"),
            RateLineLibrary.NormaliseKey("12mm sheet"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[AI]")]
    public void AnEmptyNameMatchesNothing(string? name)
    {
        // Including a name that is nothing but a tag: CleanName empties it, and
        // an empty key must never compare equal to another empty key, or every
        // blank line would look like the same library item.
        Assert.Equal(string.Empty, RateLineLibrary.NormaliseKey(name));
        Assert.False(RateLineLibrary.SameItem(name, name));
    }
}
