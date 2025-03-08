using System.IO;
using Newtonsoft.Json;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
	public static class LabourLibraryService
	{
		private static List<LabourModel> _labourItem;

		static LabourLibraryService()
		{
			string jsonPath = Path.Combine("Data", "defaultLabours.json");
			if (File.Exists(jsonPath))
			{
				string json = File.ReadAllText(jsonPath);
				_labourItem = JsonConvert.DeserializeObject<List<LabourModel>>(json);
			}
			else
			{
				_labourItem = new List<LabourModel>();
			}
		}

		public static IEnumerable<string> GetAllLabourNames()
		{
			return _labourItem.Select(m => m.LabourName).Distinct();
		}

		public static decimal GetPrice(string labourName)
		{
			var labour = _labourItem?.FirstOrDefault(l =>
				l.LabourName.Equals( labourName, StringComparison.OrdinalIgnoreCase));
			return labour != null ? (decimal)labour.LabourPrice : 0m;
		}
	}
}
