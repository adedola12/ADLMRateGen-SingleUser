using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.Services
{
    public interface ILabourDataSource
    {
        IEnumerable<LabourModel> LoadLabours();
		void SaveLabours(IEnumerable<LabourModel> labours);
	}
}
