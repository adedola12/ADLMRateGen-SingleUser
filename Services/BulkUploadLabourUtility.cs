using System.IO;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public static class BulkUploadLabourUtility
    {
		public static void BulkUploadJsonToMongo(string jsonFilePath, string connectionString, string databaseName, string collectionName)
		{
			if (!File.Exists(jsonFilePath))
			{
				Console.WriteLine($"File not found: {jsonFilePath}");
				return;
			}

			var json = File.ReadAllText(jsonFilePath);
			var labours = JsonConvert.DeserializeObject<List<LabourModel>>(json);
			if (labours == null || labours.Count == 0)
			{
				Console.WriteLine("No labour items found in the file.");
				return;
			}

			var mongoDataSource = new LabourMongoDataSource(connectionString, databaseName, collectionName);
			mongoDataSource.SaveLabours(labours);

			Console.WriteLine($"Uploaded {labours.Count} labour items to MongoDB.");
		}
	}
}
