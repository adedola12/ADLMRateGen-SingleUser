using System.IO;
using ADLMRateGen.ViewModel.Model;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
    public class LabourJsonDataSource: ILabourDataSource
    {
        private readonly string _jsonFilePath;

		public LabourJsonDataSource(string jsonFilePath)
		{
			_jsonFilePath = jsonFilePath;
		}
		public IEnumerable<LabourModel> LoadLabours()
		{
			if (!File.Exists(_jsonFilePath))
				return new List<LabourModel>();	

			string json = File.ReadAllText(_jsonFilePath);
			var labours = JsonConvert.DeserializeObject<List<LabourModel>>(json);
			return labours ?? new List<LabourModel>();

		}
		public void SaveLabours(IEnumerable<LabourModel> labours)
		{
			var directory = Path.GetDirectoryName(_jsonFilePath);
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			string json = JsonConvert.SerializeObject(labours, Formatting.Indented);
			File.WriteAllText(_jsonFilePath, json);
		}

	}
}
