using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.CustomRate;
using Xunit;

namespace ADLMRateGen.Tests;

/// <summary>
/// Regression cover for the bug where an AI-built rate saved with every price
/// at 0.00.
///
/// The AI service returned correct prices (verified against its own cache), and
/// the SDK deserialized them correctly. They were destroyed inside RateGen:
/// RateEntryItem.ResolveUnitPrice looked up the raw description — so a line
/// reading "Cement (Portland 42.5R) [AI]" could never match its library entry —
/// and then zeroed the price on every miss. Because it is wired to the *static*
/// MaterialLibraryService.LibraryChanged event, which saving raises (Harvest
/// adds entries, then RefreshLookups reloads), every AI line in the open form
/// was wiped just before it was written to disk.
///
/// The library services hold static state, so these tests must not run
/// concurrently with anything else touching it — see TestCollections.cs.
/// </summary>
[Collection(TestCollections.LibraryState)]
public class RateEntryItemPricingTests
{
    private const string AiTag = " [AI]";

    [Fact]
    public void AiPricedLine_KeepsItsPrice_WhenTheLibraryReloads()
    {
        Library.Load();  // library knows nothing about this component

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Cement (Portland 42.5R)" + AiTag;
        item.Quantity = 9.2m;
        item.UnitPrice = 10_200m;                 // the price the AI service returned

        Assert.Equal(10_200m, item.UnitPrice);

        // Saving harvests new entries and reloads the libraries, which raises
        // LibraryChanged for every live line. This is what used to zero it.
        Library.Load();

        Assert.Equal(10_200m, item.UnitPrice);
        Assert.Equal(9.2m * 10_200m, item.TotalCost);
    }

    [Fact]
    public void AiPricedLabourLine_KeepsItsPrice_WhenTheLibraryReloads()
    {
        Library.Load();

        var item = new RateEntryItem { RateType = RateItemType.Labour };
        item.Description = "Placing crew labour (pro-rated)" + AiTag;
        item.Quantity = 0.126m;
        item.UnitPrice = 17_450m;

        Library.Load();

        Assert.Equal(17_450m, item.UnitPrice);
    }

    [Fact]
    public void TaggedDescription_ResolvesAgainstTheCleanLibraryName()
    {
        // Harvest stores the clean name, so a tagged line must find it.
        Library.Load(materials: new[] { Library.Material("Cement (Portland 42.5R)", 10_200m, "bag") });

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Cement (Portland 42.5R)" + AiTag;

        Assert.Equal(10_200m, item.UnitPrice);
        Assert.Equal("bag", item.Unit);
    }

    [Fact]
    public void PlantTaggedDescription_ResolvesAgainstTheCleanLibraryName()
    {
        // Plant lines carry two tags: "Poker vibrator (plant) [AI]".
        Library.Load(materials: new[] { Library.Material("Poker vibrator", 1_250m, "hr") });

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Poker vibrator (plant)" + AiTag;

        Assert.Equal(1_250m, item.UnitPrice);
    }

    [Fact]
    public void LibraryPrice_WinsOverAnAiPrice_WhenTheItemIsKnown()
    {
        Library.Load(materials: new[] { Library.Material("Sand", 12_500m, "m3") });

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Sand" + AiTag;
        item.UnitPrice = 9_999m;      // an AI guess

        Library.Load(materials: new[] { Library.Material("Sand", 12_500m, "m3") });

        Assert.Equal(12_500m, item.UnitPrice);
    }

    [Fact]
    public void ChangingToAnUnknownItem_StillClearsTheStalePrice()
    {
        // The fix must not over-correct: picking a different item has to drop
        // the previous item's price, or the line silently keeps the wrong one.
        Library.Load();

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Something the library does not know";
        item.UnitPrice = 500m;

        item.Description = "A different thing the library also does not know";

        Assert.Equal(0m, item.UnitPrice);
    }

    [Fact]
    public void ChangingToAKnownItem_AdoptsTheLibraryPrice()
    {
        Library.Load(materials: new[]
        {
            Library.Material("Cement", 10_200m, "bag"),
            Library.Material("Sand", 12_500m, "m3"),
        });

        var item = new RateEntryItem { RateType = RateItemType.Material };
        item.Description = "Cement";
        Assert.Equal(10_200m, item.UnitPrice);

        item.Description = "Sand";
        Assert.Equal(12_500m, item.UnitPrice);
        Assert.Equal("m3", item.Unit);
    }

    [Fact]
    public void ReloadingRateItems_RestoresSavedPrices_EvenWhenTheLibraryIsEmpty()
    {
        // Mirrors CustomRateEntryViewModel.LoadRate: RateType first, UnitPrice
        // last. Setting RateType last used to re-trigger the lookup and zero the
        // price of every saved line the library does not know.
        Library.Load();

        var reloaded = new RateEntryItem
        {
            RateType = RateItemType.Labour,
            Description = "Mixing plant and labour (pro-rated)" + AiTag,
            Quantity = 0.126m,
            Unit = "day",
            UnitPrice = 11_969.51m,
        };

        Assert.Equal(11_969.51m, reloaded.UnitPrice);
    }
}
