using System.IO;
using ADLMRateGen.ViewModel.CustomRate;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
	public class CustomRateServices
    {
		public static event Action<CustomRate> OnCustomRateSaved;

		private static readonly string FilePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"MyApp", "CustomRates.json");

		public static void SaveCustomRate(CustomRate rate)
		{
			var rates = LoadCustomRates().ToList();
			rates.Add(rate);
			SaveRates(rates);

			OnCustomRateSaved?.Invoke(rate);
		}

		public static IEnumerable<CustomRate> LoadCustomRates()
		{
			if (!File.Exists(FilePath))
			{
				return new List<CustomRate>();
			}

			var json = File.ReadAllText(FilePath);
			return JsonConvert.DeserializeObject<List<CustomRate>>(json) ?? new List<CustomRate>();
		}

		public static void SaveRates(IEnumerable<CustomRate> rates)
		{
			var directory = Path.GetDirectoryName(FilePath);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			var json = JsonConvert.SerializeObject(rates, Formatting.Indented);
			File.WriteAllText(FilePath, json);
		}
	}
}
