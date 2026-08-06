using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Tests;

/// <summary>
/// In-memory material library, so tests never read or write the real
/// materials.json in the user's AppData.
/// </summary>
internal sealed class InMemoryMaterialSource : IMaterialDataSource
{
    private List<MaterialModel> _materials;

    public InMemoryMaterialSource(params MaterialModel[] materials) =>
        _materials = materials.ToList();

    public IEnumerable<MaterialModel> LoadMaterials() => _materials.ToList();

    public void SaveMaterials(IEnumerable<MaterialModel> materials) =>
        _materials = materials.ToList();
}

/// <summary>In-memory labour library. See <see cref="InMemoryMaterialSource"/>.</summary>
internal sealed class InMemoryLabourSource : ILabourDataSource
{
    private List<LabourModel> _labours;

    public InMemoryLabourSource(params LabourModel[] labours) =>
        _labours = labours.ToList();

    public IEnumerable<LabourModel> LoadLabours() => _labours.ToList();

    public void SaveLabours(IEnumerable<LabourModel> labours) =>
        _labours = labours.ToList();
}

internal static class Library
{
    public static MaterialModel Material(string name, decimal price, string unit = "") =>
        new() { MaterialName = name, MaterialPrice = price, MaterialUnit = unit };

    public static LabourModel Labour(string name, decimal price, string unit = "") =>
        new() { LabourName = name, LabourPrice = price, LabourUnit = unit };

    /// <summary>Points both library services at fresh in-memory data.</summary>
    public static void Load(
        IEnumerable<MaterialModel>? materials = null,
        IEnumerable<LabourModel>? labours = null)
    {
        MaterialLibraryService.Initialize(
            new InMemoryMaterialSource((materials ?? Array.Empty<MaterialModel>()).ToArray()));
        LabourLibraryService.Initialize(
            new InMemoryLabourSource((labours ?? Array.Empty<LabourModel>()).ToArray()));
    }
}
