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

			if (found == null)
			{
				foreach (var alias in RateNameAliases.Alternates(name))
				{
					found = _materialLib.MaterialLibrary
						.FirstOrDefault(m => m.MaterialName == alias);
					if (found != null) break;
				}
			}

			return (double)(found?.MaterialPrice ?? 0);
		}

		/// <summary>
		/// A miss returns zero, which prices a build-up line at nothing and says
		/// so nowhere. That is why the library being renamed underneath a shipped
		/// build has to be survivable: fall back to the row's former spellings
		/// before giving up. See <see cref="RateNameAliases"/>.
		/// </summary>
		public double GetLabourRate(string name)
		{
			var found = _labourLib.LabourLibrary
				.FirstOrDefault(l => l.LabourName == name);

			if (found == null)
			{
				foreach (var alias in RateNameAliases.Alternates(name))
				{
					found = _labourLib.LabourLibrary
						.FirstOrDefault(l => l.LabourName == alias);
					if (found != null) break;
				}
			}

			return (double)(found?.LabourPrice ?? 0);
		}
	}


}
