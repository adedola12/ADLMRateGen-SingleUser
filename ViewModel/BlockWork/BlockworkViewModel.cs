using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.ConcreteWork;

namespace ADLMRateGen.ViewModel.BlockWork
{
    public class BlockworkViewModel: ViewModelBase
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
                if(_overheadPercent != value)
				{
					_overheadPercent = value;
					RaisePropertyChanged();
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
                }
            }
        }
        public ObservableCollection<BlockworkItem> BlockworkItems { get; set; } =
			new ObservableCollection<BlockworkItem>();
        public ICollectionView BlockworkCollectionView { get; private set; }
        public string SearchTerm
        {
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					BlockworkCollectionView.Refresh();
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
        public BlockworkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
		{
			_helper = new GetItemsFromDB(matLib, labourLib);
            matLib.LibraryChanged += OnLibraryChange;
            labourLib.LibraryChanged += OnLibraryChange;

            BuildBlockWorkItem();

			BlockworkCollectionView = CollectionViewSource.GetDefaultView(BlockworkItems);
			BlockworkCollectionView.Filter = FilterBlockWorkItem;

			RecomputeCommand = new DelegateCommand(o => RecomputeAll());
			ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
		}

		#region Function Method
		private void ShowDetails(object o)
		{
			if(o is BlockworkItem item)
			{
				var detailedControl = new BlockworkItemDetailControl();
				detailedControl.DataContext = item;

				detailedControl.BackRequested += () =>
				{
					SelectedDetail = null;
				};

				SelectedDetail = detailedControl;
			}
		}
		private bool FilterBlockWorkItem(object obj)
		{
			if (obj is BlockworkItem item)
			{
				if(string.IsNullOrEmpty(SearchTerm))
				{
					return true;
				}
					return item.Description?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
			}

			return false;
		}
		private void RecomputeAll()
		{
			BlockworkItems.Clear();
			BuildBlockWorkItem();
		}
		private void OnLibraryChange()
		{
			RecomputeAll();
		}
		private void BuildBlockWorkItem()
		{
			Func<BlockworkItem>[] computeMethods =
			{
				ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,
				//ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
			};
			foreach (var compute in computeMethods)
			{
				BlockworkItems.Add(compute());
			}
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
		#endregion

		#region Compute Methods
		private BlockworkItem ComputeItem1()
		{
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 40;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + ( operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerm3 = costPerHr / volPerHr;
			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				new BlockworkBreakdownLine { ComponentName="Concrete mixer 10/7", Quantity=1, Unit="N/day", UnitPrice=mixerCost },
				new BlockworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=literPerDay, Unit="hr/m3", UnitPrice=dieselPrice, TotalPrice=fuelCost },
				new BlockworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=3, Unit="%", TotalPrice=0.03 * fuelCost },
				new BlockworkBreakdownLine { ComponentName="Operator (per day)", Quantity=1, Unit="Nr/Day", UnitPrice=operatorCost, TotalPrice=operatorCost },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Cost per day", Quantity=1, Unit="", TotalPrice=totalPlantDay },
				new BlockworkBreakdownLine { ComponentName="Cost per hour (8 hour Working Day)", Quantity=workHr, Unit="N/Hr", UnitPrice=totalPlantDay, TotalPrice=costPerHr },
				new BlockworkBreakdownLine { ComponentName="Total", Quantity=volPerHr, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new BlockworkItem
			{
				ItemNo = 1,
				Description = "Calculating plant and labour cost for mixing concrete or mortar, using 14/10 mixer. Note mixer is running a 3 minute circle prior to pouring out mix.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem2()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");

			double cementPerM3 = 28.82;
			double sandPerM3 = 3;
			double wastePer = 25;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			double materialCostPerCum = finalMaterialCost / 4;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = GetLabourRate("Labourer") / 8;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerUse = costPerHr / volPerHr;


			double netCostPerm3 = materialCostPerCum + netCostPerUse;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new BlockworkItem
			{
				ItemNo = 2,
				Description = "Mortar Mix (1:3)",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem3()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");

			double cementPerM3 = 28.82;
			double sandPerM3 = 4;
			double wastePer = 25;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			double materialCostPerCum = finalMaterialCost / 5;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = GetLabourRate("Labourer") / 8;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerUse = costPerHr / volPerHr;


			double netCostPerm3 = materialCostPerCum + netCostPerUse;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new BlockworkItem
			{
				ItemNo = 3,
				Description = "Mortar Mix (1:4)",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem4()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");

			double cementPerM3 = 28.82;
			double sandPerM3 = 6;
			double wastePer = 25;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			double materialCostPerCum = finalMaterialCost / 7;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = GetLabourRate("Labourer") / 8;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerUse = costPerHr / volPerHr;


			double netCostPerm3 = materialCostPerCum + netCostPerUse;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new BlockworkItem
			{
				ItemNo = 4,
				Description = "Mortar Mix (1:6)",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem5()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");

			double cementPerM3 = 28.82;
			double sandPerM3 = 1;
			double wastePer = 25;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			double materialCostPerCum = finalMaterialCost / 2;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = GetLabourRate("Labourer") / 8;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerUse = costPerHr / volPerHr;


			double netCostPerm3 = materialCostPerCum + netCostPerUse;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new BlockworkItem
			{
				ItemNo = 5,
				Description = "Mortar Mix (1:1)",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}

		private BlockworkItem ComputeItem6()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");

			double cementPerM3 = 28.82;
			double sandPerM3 = 12;
			double wastePer = 25;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			double materialCostPerCum = finalMaterialCost / 13;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = GetLabourRate("Labourer") / 8;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double netCostPerUse = costPerHr / volPerHr;


			double netCostPerm3 = materialCostPerCum + netCostPerUse;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new BlockworkItem
			{
				ItemNo = 6,
				Description = "Mortar Mix (1:12)",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}

		//private BlockworkItem ComputeItem7()
		//{
		//	throw new NotImplementedException();
		//}

		//private BlockworkItem ComputeItem8()
		//{
		//	throw new NotImplementedException();
		//}

		//private BlockworkItem ComputeItem9()
		//{
		//	throw new NotImplementedException();
		//}

		//private BlockworkItem ComputeItem10()
		//{
		//	throw new NotImplementedException();
		//}

		//private BlockworkItem ComputeItem11()
		//{
		//	throw new NotImplementedException();
		//}

		//private BlockworkItem ComputeItem12()
		//{
		//	throw new NotImplementedException();
		//}

		#endregion

	}
}
