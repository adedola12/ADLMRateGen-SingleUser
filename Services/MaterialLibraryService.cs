using System.IO;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public static class MaterialLibraryService
    {
		private static IMaterialDataSource _dataSource;
		private static List<MaterialModel> _materials;

		/// <summary>
		/// Initialize the service with a data source.
		/// </summary>
		public static void Initialize(IMaterialDataSource dataSource)
		{
			_dataSource = dataSource;
			_materials = _dataSource.LoadMaterials().ToList();
		}

		/// <summary>
		/// Returns all distinct material names.
		/// </summary>
		public static IEnumerable<string> GetAllMaterialNames()
		{
			return _materials?.Select(m => m.MaterialName).Distinct() ?? Enumerable.Empty<string>();
		}

		/// <summary>
		/// Returns the price for a given material name.
		/// </summary>
		public static decimal GetPrice(string materialName)
		{
			var mat = _materials?.FirstOrDefault(m =>
				m.MaterialName.Equals(materialName, StringComparison.OrdinalIgnoreCase));
			return mat != null ? (decimal)mat.MaterialPrice : 0m;
		}

		/// <summary>
		/// Returns all materials.
		/// </summary>
		public static IEnumerable<MaterialModel> GetAllMaterials() => _materials ?? Enumerable.Empty<MaterialModel>();

		/// <summary>
		/// Replace the entire collection and persist changes.
		/// </summary>
		public static void AddOrUpdateMaterials(IEnumerable<MaterialModel> newMaterials)
		{
			_materials = newMaterials.ToList();
			_dataSource.SaveMaterials(_materials);
		}


		//static MaterialLibraryService()
		//{
		//	// On startup, load from defaultMaterials.json
		//	string jsonPath = Path.Combine("Data", "defaultMaterials.json");
		//	if (File.Exists(jsonPath))
		//	{
		//		string json = File.ReadAllText(jsonPath);
		//		_materials = JsonConvert.DeserializeObject<List<MaterialModel>>(json);
		//	}
		//	else
		//	{
		//		_materials = new List<MaterialModel>();
		//	}
		//}
		//public static IEnumerable<string> GetAllMaterialNames()
		//{
		//	return _materials.Select(m => m.MaterialName).Distinct();
		//}

		//public static decimal GetPrice(string materialName)
		//{
		//	var mat = _materials.FirstOrDefault(m =>
		//	  m.MaterialName.Equals(materialName, StringComparison.OrdinalIgnoreCase));
		//	return mat != null ? (decimal)mat.MaterialPrice : 0m;
		//}
	}
}
