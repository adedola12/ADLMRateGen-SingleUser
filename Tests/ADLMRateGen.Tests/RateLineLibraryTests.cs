using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.CustomRate;
using Xunit;

namespace ADLMRateGen.Tests;

[Collection(TestCollections.LibraryState)]
public class RateLineLibraryTests
{
    [Theory]
    [InlineData("Cement", "Cement")]
    [InlineData("Cement [AI]", "Cement")]
    [InlineData("Poker vibrator (plant)", "Poker vibrator")]
    [InlineData("Poker vibrator (plant) [AI]", "Poker vibrator")]
    [InlineData("  Sand [AI]  ", "Sand")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void CleanName_StripsProvenanceTags(string? description, string expected) =>
        Assert.Equal(expected, RateLineLibrary.CleanName(description));

    [Fact]
    public void CleanName_LeavesGenuineBracketsAlone()
    {
        // Only the known trailing tags are stripped — a name that legitimately
        // ends in brackets must survive intact.
        Assert.Equal(
            "Cement (Portland 42.5R)",
            RateLineLibrary.CleanName("Cement (Portland 42.5R)"));
    }

    [Fact]
    public void Harvest_AddsPricedLinesUnderTheirCleanName()
    {
        Library.Load();

        var material = new RateEntryItem { RateType = RateItemType.Material };
        material.Description = "Cement (Portland 42.5R) [AI]";
        material.Unit = "bag";
        material.UnitPrice = 10_200m;

        var labour = new RateEntryItem { RateType = RateItemType.Labour };
        labour.Description = "Placing crew labour [AI]";
        labour.UnitPrice = 17_450m;

        var added = RateLineLibrary.Harvest(new[] { material }, new[] { labour });

        Assert.Equal(2, added);
        Assert.Equal(10_200m, MaterialLibraryService.GetPrice("Cement (Portland 42.5R)"));
        Assert.Equal(17_450m, LabourLibraryService.GetPrice("Placing crew labour"));
    }

    [Fact]
    public void Harvest_SkipsUnpricedLines()
    {
        // A zero-priced line is not a price worth remembering.
        Library.Load();

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Unpriced thing [AI]";

        Assert.Equal(0, RateLineLibrary.Harvest(new[] { item }, Array.Empty<RateEntryItem>()));
        Assert.Null(MaterialLibraryService.FindByName("Unpriced thing"));
    }

    [Fact]
    public void Harvest_NeverOverwritesAnExistingLibraryPrice()
    {
        // A one-off rate must not silently rewrite the master price list.
        Library.Load(materials: new[] { Library.Material("Sand", 12_500m, "m3") });

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Sand";
        item.UnitPrice = 999m;

        var added = RateLineLibrary.Harvest(new[] { item }, Array.Empty<RateEntryItem>());

        Assert.Equal(0, added);
        Assert.Equal(12_500m, MaterialLibraryService.GetPrice("Sand"));
    }
}
