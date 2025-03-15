using System.IO;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public static class BulkUploadUtility
    {
        public static void BulkUploadJsonToMongo(string jsonFilePath, string connectionString, string databaseName, string collectionName)
		{
			if(!File.Exists(jsonFilePath))
			{
				Console.WriteLine($"File not found: {jsonFilePath}");
				return;
			}

			var json = File.ReadAllText(jsonFilePath);
			var materials = JsonConvert.DeserializeObject<List<MaterialModel>>(json);
			if (materials == null || materials.Count == 0)
			{
				Console.WriteLine("No materials found in the file.");
				return;
			}

			var mongoDataSource = new MaterialMongoDataSource(connectionString, databaseName, collectionName);

			mongoDataSource.SaveMaterials(materials);

			Console.WriteLine($"Uploaded {materials.Count} materials to MongoDB.");
		}
	}
}
