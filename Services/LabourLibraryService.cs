using System.IO;
using Newtonsoft.Json;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
	public static class LabourLibraryService
	{
		private static ILabourDataSource _dataSource;
		private static List<LabourModel> _labours;

		/// <summary>
		/// Initialize the service with a data source (either MongoDB or JSON).
		/// </summary>
		public static void Initialize(ILabourDataSource dataSource)
		{
			_dataSource = dataSource;
			_labours = _dataSource.LoadLabours().ToList();
		}

		public static IEnumerable<string> GetAllLabourNames()
		{
			return _labours?.Select(l => l.LabourName).Distinct() ?? Enumerable.Empty<string>();
		}

		public static decimal GetPrice(string labourName)
		{
			var labour = _labours?.FirstOrDefault(l =>
				l.LabourName.Equals(labourName, StringComparison.OrdinalIgnoreCase));
			return labour != null ? labour.LabourPrice : 0m;
		}

		public static IEnumerable<LabourModel> GetAllLabours() => _labours ?? Enumerable.Empty<LabourModel>();

		/// <summary>
		/// Replace the entire collection and persist changes.
		/// </summary>
		public static void AddOrUpdateLabours(IEnumerable<LabourModel> newLabours)
		{
			_labours = newLabours.ToList();
			_dataSource.SaveLabours(_labours);
		}
	}
}
