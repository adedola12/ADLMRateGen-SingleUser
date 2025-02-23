using ADLMRateGen.ViewModel;

namespace ADLMRateGen.Helpers
{

	public class GetItemsFromDB
	{
		private readonly MaterialLibraryViewModel _materialLib;
		private readonly LabourLibraryViewModel _labourLib;

		public GetItemsFromDB(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
		{
			_materialLib = matLib;
			_labourLib = labourLib;
		}

		public double GetMaterialPrice(string name)
		{
			var found = _materialLib.MaterialLibrary
				.FirstOrDefault(m => m.MaterialName == name);
			return (double)(found?.MaterialPrice ?? 0);
		}
		public double GetLabourRate(string name)
		{
			var found = _labourLib.LabourLibrary
				.FirstOrDefault(l => l.LabourName == name);
			return (double)(found?.LabourPrice ?? 0);
		}
	}
}
