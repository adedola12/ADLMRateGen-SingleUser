using System.IO;
using ADLMRateGen.ViewModel.CustomRate;
using Newtonsoft.Json;

namespace ADLMRateGen.Services
{
	public static class CustomRateServices
	{
		private static readonly string FilePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"MyApp", "CustomRates.json");

		// Event fired when a new rate is saved
		public static event Action<CustomRate> OnCustomRateSaved;

		// Event fired when an existing rate is updated
		public static event Action<CustomRate> OnCustomRateUpdated;

		public static void SaveCustomRate(CustomRate rate)
		{
			var rates = LoadCustomRates().ToList();
			rates.Add(rate);
			SaveRates(rates);

			OnCustomRateSaved?.Invoke(rate);
		}

		/// <summary>
		/// Update an existing CustomRate by matching on Id.
		/// If not found, adds it. Then fires OnCustomRateUpdated.
		/// </summary>
		public static void UpdateCustomRate(CustomRate updatedRate)
		{
			var rates = LoadCustomRates().ToList();

			// Try to find the existing rate by Id
			var existing = rates.FirstOrDefault(r => r.Id == updatedRate.Id);
			if (existing != null)
			{
				// Update each field
				existing.Title = updatedRate.Title;
				existing.Description = updatedRate.Description;
				existing.OverheadPercent = updatedRate.OverheadPercent;
				existing.ProfitPercent = updatedRate.ProfitPercent;
				existing.MaterialItems = updatedRate.MaterialItems;
				existing.LabourItems = updatedRate.LabourItems;
			}
			else
			{
				// If no match, add it as new
				rates.Add(updatedRate);
			}

			SaveRates(rates);

			// Invoke the 'Updated' event (not the 'Saved' event)
			OnCustomRateUpdated?.Invoke(updatedRate);
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
