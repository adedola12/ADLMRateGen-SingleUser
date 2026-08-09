using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;

namespace ADLMRateGen.ViewModel.ConcreteWork
{
	public class ConcreteWorkViewModel : ViewModelBase
	{
		private readonly GetItemsFromDB _helper;

		private double _overheadPercent = 10.0;
		private double _profitPercent = 25.0;
		private string _searchTerm = string.Empty;
		private object _selectedDetail;

		public double OverheadPercent
		{
			get => _overheadPercent;
			set
			{
				if (_overheadPercent != value)
				{
					_overheadPercent = value;
					RaisePropertyChanged();
					RecomputeAll();
				}
			}
		}
		public double ProfitPercent
		{
			get => _profitPercent;
			set
			{
				if (_profitPercent != value)
				{
					_profitPercent = value;
					RaisePropertyChanged();
					RecomputeAll();
				}
			}
		}
		public ObservableCollection<ConcreteworkItem> ConcreteWorkItems { get; set; }
			= new ObservableCollection<ConcreteworkItem>();
		public ICollectionView ConcreteworkCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					ConcreteworkCollectionView.Refresh();
				}
			}
		}
		public object SelectedDetail
		{
			get => _selectedDetail;
			set
			{
				if (_selectedDetail != value)
				{
					_selectedDetail = value;
					RaisePropertyChanged();
				}
			}
		}
		public ICommand RecomputeCommand { get; }
		public ICommand ShowDetailsCommand { get; }
		public ConcreteWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourlib)
		{
			_helper = new GetItemsFromDB(matLib, labourlib);
			matLib.LibraryChanged += OnLibraryChanged;
			labourlib.LibraryChanged += OnLibraryChanged;

			BuildConcreteWorkItem();

			ConcreteworkCollectionView = CollectionViewSource.GetDefaultView(ConcreteWorkItems);
			ConcreteworkCollectionView.Filter = FilterConcreteWorkItem;

			RecomputeCommand = new DelegateCommand(o => RecomputeAll());
			ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
		}
		private void ShowDetails(object o)
		{
			if (o is ConcreteworkItem item)
			{
				var detailedControl = new ConcreteworkItemDetailControl();
				detailedControl.DataContext = item;

				detailedControl.BackRequested += () =>
				{
					SelectedDetail = null;
				};

				SelectedDetail = detailedControl;
			}
		}
		private bool FilterConcreteWorkItem(object obj)
		{
			if (obj is ConcreteworkItem item)
			{
				if (string.IsNullOrEmpty(SearchTerm))
				{
					return true;
				}
				return item.Description?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;

			}
			return false;
		}
		private void OnLibraryChanged()
		{
			RecomputeAll();
		}
		private void RecomputeAll()
		{
			ConcreteWorkItems.Clear();
			BuildConcreteWorkItem();
		}
		private void BuildConcreteWorkItem()
		{
			ConcreteWorkItems.Add(ComputeItem1());
		}
		private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
		{
			double ov = netCost * (OverheadPercent / 100);
			double pv = netCost * (ProfitPercent / 100);
			double total = netCost + ov + pv;

			return (ov, pv, total);
		}
		private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
		private double GetLabourRate(string name) => _helper.GetLabourRate(name);
		private ConcreteworkItem ComputeItem1()
		{
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = GetMaterialPrice("Diesel");
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerm3 = costPerHr / volPerHr;
			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				new ConcreteworkBreakdownLine { ComponentName="Concrete 10/14 mixer.", Quantity=1, Unit="N/day", UnitPrice=mixerCost },
				new ConcreteworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=literPerDay, Unit="hr/m3", UnitPrice=dieselPrice, TotalPrice=fuelCost },
				new ConcreteworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=1, Unit="3%", TotalPrice=0.03 * fuelCost },
				new ConcreteworkBreakdownLine { ComponentName="Operator (per day)", Quantity=2, Unit="Nr/Day", UnitPrice=operatorCost, TotalPrice=operatorCost*2 },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Cost per day", Quantity=1, Unit="", TotalPrice=totalPlantDay },

				new ConcreteworkBreakdownLine { ComponentName="Cost per hour (8 hour Working Day)", Quantity=8, Unit="N/Hr", UnitPrice=totalPlantDay, TotalPrice=costPerHr },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=5.66, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 1,
				Description = "Calculating plant and labour cost for mixing concrete," +
				" using 21/14 mixer. Note: mixer is running a 3 minute circle prior " +
				"to pouring out mix.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}


	}
}
