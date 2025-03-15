using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
    public interface IMaterialDataSource
    {
		IEnumerable<MaterialModel> LoadMaterials();
		void SaveMaterials(IEnumerable<MaterialModel> materials);
	}
}
