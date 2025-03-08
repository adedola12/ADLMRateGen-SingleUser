using System.IO;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public static class MaterialLibraryService
    {
		private static List<MaterialModel> _materials;
		static MaterialLibraryService()
		{
			// On startup, load from defaultMaterials.json
			string jsonPath = Path.Combine("Data", "defaultMaterials.json");
			if (File.Exists(jsonPath))
			{
				string json = File.ReadAllText(jsonPath);
				_materials = JsonConvert.DeserializeObject<List<MaterialModel>>(json);
			}
			else
			{
				_materials = new List<MaterialModel>();
			}
		}
		public static IEnumerable<string> GetAllMaterialNames()
		{
			return _materials.Select(m => m.MaterialName).Distinct();
		}

		public static decimal GetPrice(string materialName)
		{
			var mat = _materials.FirstOrDefault(m =>
			  m.MaterialName.Equals(materialName, StringComparison.OrdinalIgnoreCase));
			return mat != null ? (decimal)mat.MaterialPrice : 0m;
		}
	}
}
