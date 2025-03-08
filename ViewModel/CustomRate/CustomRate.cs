using System.Text.Json.Serialization;

namespace ADLMRateGen.ViewModel.CustomRate
{
	public class CustomRate
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string Description { get; set; }
		public List<RateEntryItem> MaterialItems { get; set; } = new List<RateEntryItem>();
		public List<RateEntryItem> LabourItems { get; set; } = new List<RateEntryItem>();
		public decimal OverheadPercent { get; set; }
		public decimal ProfitPercent { get; set; }
		public DateTime CreatedDate { get; set; } = DateTime.Now;

		[JsonIgnore]
		public decimal TotalMaterialCost => MaterialItems.Sum(item => item.TotalCost);
		[JsonIgnore]
		public decimal TotalLabourCost => LabourItems.Sum(item => item.TotalCost);
		[JsonIgnore]
		public decimal OverallTotal => TotalMaterialCost + TotalLabourCost;
		[JsonIgnore]
		public decimal GrandTotal => OverallTotal * (1 + (OverheadPercent + ProfitPercent) / 100);
	}
}
