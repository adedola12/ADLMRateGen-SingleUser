using System.IO;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public class MaterialJsonDataSource: IMaterialDataSource
    {
        private readonly string _jsonFilePath;
		public MaterialJsonDataSource(string jsonFilePath)
		{
			_jsonFilePath = jsonFilePath;
		}

		public IEnumerable<MaterialModel> LoadMaterials()
		{
			if (!File.Exists(_jsonFilePath))
			{
				return new List<MaterialModel>();
			}

			string json = File.ReadAllText(_jsonFilePath);
			var materials = JsonConvert.DeserializeObject<List<MaterialModel>>(json);
			return materials ?? new List<MaterialModel>();
		}

		public void SaveMaterials(IEnumerable<MaterialModel> materials)
		{
			var directory = Path.GetDirectoryName(_jsonFilePath);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			string json = JsonConvert.SerializeObject(materials, Formatting.Indented);
			File.WriteAllText(_jsonFilePath, json);
		}
	}
}
