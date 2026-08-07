using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.CustomRate;
using Xunit;

namespace ADLMRateGen.Tests;

/// <summary>
/// The material and labour libraries legitimately hold several rows under one
/// name — the shipped defaultMaterials.json carries 500 rows for 477 distinct
/// names, differing by unit or category.
///
/// AddOrUpdate* used to rebuild the list through a name-keyed dictionary, which
/// threw ArgumentException on the first duplicate and, had it not thrown, would
/// have written back only one row per name. Saving a custom rate calls this, so
/// the whole library was one save away from losing its duplicates.
/// </summary>
[Collection(TestCollections.LibraryState)]
public class LibraryDuplicateNameTests
{
    [Fact]
    public void AddOrUpdateMaterials_DoesNotThrow_WhenTheLibraryHasDuplicateNames()
    {
        Library.Load(materials: new[]
        {
            Library.Material("Cement", 10_000m, "bag"),
            Library.Material("Cement", 11_000m, "tonne"),   // same name, different unit
        });

        // The exception this used to throw was swallowed by the caller's
        // try/catch, so harvesting silently did nothing.
        MaterialLibraryService.AddOrUpdateMaterials(new[] { Library.Material("Sand", 12_500m, "m3") });

        Assert.Equal(3, MaterialLibraryService.GetAllMaterials().Count());
    }

    [Fact]
    public void AddOrUpdateMaterials_KeepsEveryDuplicateRow()
    {
        Library.Load(materials: new[]
        {
            Library.Material("Cement", 10_000m, "bag"),
            Library.Material("Cement", 11_000m, "tonne"),
            Library.Material("Sand", 12_500m, "m3"),
        });

        MaterialLibraryService.AddOrUpdateMaterials(new[] { Library.Material("Granite", 33_000m, "m3") });

        var all = MaterialLibraryService.GetAllMaterials().ToList();
        Assert.Equal(4, all.Count);
        Assert.Equal(2, all.Count(m => m.MaterialName == "Cement"));
        // Units on the duplicates must survive, not be flattened to one row.
        Assert.Contains(all, m => m.MaterialName == "Cement" && m.MaterialUnit == "bag");
        Assert.Contains(all, m => m.MaterialName == "Cement" && m.MaterialUnit == "tonne");
    }

    [Fact]
    public void AddOrUpdateMaterials_UpdatesEveryRowSharingTheName()
    {
        Library.Load(materials: new[]
        {
            Library.Material("Cement", 10_000m, "bag"),
            Library.Material("Cement", 11_000m, "bag"),
        });

        MaterialLibraryService.AddOrUpdateMaterials(new[] { Library.Material("Cement", 12_345m) });

        var cement = MaterialLibraryService.GetAllMaterials().Where(m => m.MaterialName == "Cement").ToList();
        Assert.Equal(2, cement.Count);
        Assert.All(cement, m => Assert.Equal(12_345m, m.MaterialPrice));
    }

    [Fact]
    public void AddOrUpdateLabours_KeepsEveryDuplicateRow()
    {
        Library.Load(labours: new[]
        {
            Library.Labour("Mason", 14_000m, "day"),
            Library.Labour("Mason", 1_750m, "hr"),
        });

        LabourLibraryService.AddOrUpdateLabours(new[] { Library.Labour("Headman", 12_000m, "day") });

        var all = LabourLibraryService.GetAllLabours().ToList();
        Assert.Equal(3, all.Count);
        Assert.Equal(2, all.Count(l => l.LabourName == "Mason"));
    }

    [Fact]
    public void SavingARate_DoesNotShrinkALibraryThatHasDuplicates()
    {
        // The end-to-end shape of the bug: harvest runs on save, and the
        // library must come back the same size plus whatever was genuinely new.
        Library.Load(materials: new[]
        {
            Library.Material("Cement", 10_000m, "bag"),
            Library.Material("Cement", 11_000m, "tonne"),
            Library.Material("Sand", 12_500m, "m3"),
        });

        var before = MaterialLibraryService.GetAllMaterials().Count();

        var line = new RateEntryItem { RateType = RateItemType.Material };
        line.Description = "Granite (including transportation) [AI]";
        line.Unit = "m3";
        line.UnitPrice = 33_000m;

        var added = RateLineLibrary.Harvest(new[] { line }, System.Array.Empty<RateEntryItem>());

        Assert.Equal(1, added);
        Assert.Equal(before + 1, MaterialLibraryService.GetAllMaterials().Count());
    }
}
