using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.ConcreteWork
{
    public class ConcreteViewModel: ViewModelBase
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
		public ConcreteViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourlib)
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
			ConcreteWorkItems.Add(ComputeItem2());
			ConcreteWorkItems.Add(ComputeItem3());
			ConcreteWorkItems.Add(ComputeItem4());
			ConcreteWorkItems.Add(ComputeItem5());
			ConcreteWorkItems.Add(ComputeItem6());
			ConcreteWorkItems.Add(ComputeItem7());
			ConcreteWorkItems.Add(ComputeItem8());
			ConcreteWorkItems.Add(ComputeItem9());
			ConcreteWorkItems.Add(ComputeItem10());
			ConcreteWorkItems.Add(ComputeItem11());
			ConcreteWorkItems.Add(ComputeItem12());
			ConcreteWorkItems.Add(ComputeItem13());
			ConcreteWorkItems.Add(ComputeItem14());
			ConcreteWorkItems.Add(ComputeItem15());
			ConcreteWorkItems.Add(ComputeItem16());
			ConcreteWorkItems.Add(ComputeItem17());
			ConcreteWorkItems.Add(ComputeItem18());
			ConcreteWorkItems.Add(ComputeItem19());
			//ConcreteWorkItems.Add(ComputeItem20());
			//ConcreteWorkItems.Add(ComputeItem21());
			//ConcreteWorkItems.Add(ComputeItem22());
			//ConcreteWorkItems.Add(ComputeItem23());
			//ConcreteWorkItems.Add(ComputeItem24());
			//ConcreteWorkItems.Add(ComputeItem25());
			//ConcreteWorkItems.Add(ComputeItem26());
			//ConcreteWorkItems.Add(ComputeItem27());
			//ConcreteWorkItems.Add(ComputeItem28());
			//ConcreteWorkItems.Add(ComputeItem29());
			//ConcreteWorkItems.Add(ComputeItem30());
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
			double dieselPrice = GetLabourRate("Labourer") / 8;
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
		private ConcreteworkItem ComputeItem2()
		{
			double mixerCost = GetLabourRate("Concrete mixer 21/14");
			double dieselPrice = (GetLabourRate("Labourer") / 8) *1.4;
			double literPerDay = 40;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.2;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 7.94;
			double netCostPerm3 = costPerHr / volPerHr;
			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				new ConcreteworkBreakdownLine { ComponentName="Concrete mixer 21/14", Quantity=1, Unit="N/day", UnitPrice=mixerCost },
				new ConcreteworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=literPerDay, Unit="hr/m3", UnitPrice=dieselPrice, TotalPrice=fuelCost },
				new ConcreteworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=1, Unit="3%", TotalPrice=0.03 * fuelCost },
				new ConcreteworkBreakdownLine { ComponentName="Operator (per day)", Quantity=2, Unit="Nr/Day", UnitPrice=operatorCost, TotalPrice=operatorCost*2 },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Cost per day", Quantity=1, Unit="", TotalPrice=totalPlantDay },

				new ConcreteworkBreakdownLine { ComponentName="Cost per hour (8 hour Working Day)", Quantity=workHr, Unit="N/Hr", UnitPrice=totalPlantDay, TotalPrice=costPerHr },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=volPerHr, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 2,
				Description = "Calculating plant and labour cost for mixing concrete, using 21/14 mixer. Note mixer is running a 3 minute circle prior to pouring out mix.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem3()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			//double stonePrice = GetMaterialPrice("Washed gravel (local)") ;
			double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling")* 0.474;

			double cementPerM3 = 3.32;
			double sandPerM3 = 0.46;
			double stonePerM3 = 0.90;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 6;
			double mixingCrewCost = ((GetLabourRate("Labourer") ) );
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)");
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 6;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost+totalPlacingCrewCost+totalMasonCrewCost+totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 3,
				Description = "Concrete (1:4:8) grade 10 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem4()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 4.32;
			double sandPerM3 = 0.45;
			double stonePerM3 = 0.90;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 6;
			double mixingCrewCost = ((GetLabourRate("Labourer")/8)*1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)")/8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 6;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 4,
				Description = "Concrete (1:3:6) grade 15 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem5()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 6.16;
			double sandPerM3 = 0.43;
			double stonePerM3 = 0.86;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 6;
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 6;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 5,
				Description = "Concrete (1:2:4) grade 20 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem6()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 7.86;
			double sandPerM3 = 0.41;
			double stonePerM3 = 0.82;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 6;
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 6;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 6,
				Description = "Concrete (1:1.5:3) grade 25 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem7()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 6.16;
			double sandPerM3 = 0.43;
			double stonePerM3 = 0.86;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 9;
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 12;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },
			
				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },
			
			
				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 7,
				Description = "Concrete (1:2:4) grade 20 in column, wall or suspended floor.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem8()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 7.86;
			double sandPerM3 = 0.41;
			double stonePerM3 = 0.82;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 9;
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 12;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },
			
				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 8,
				Description = "Concrete (1:1.5:3) grade 25 in column, wall or suspended floor.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem9()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 18;
			double sandPerM3 = 0.3;
			double stonePerM3 = 0.6;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 9;
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 0.24;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 9;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 2;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },
			
				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },
			
			
				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 9,
				Description = "Concrete (1:1/2:1) grade 35 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem10()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 4.2;
			double sandPerM3 = 0.6;
			double stonePerM3 = 0.74;
			double wastePer = 5;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			//LABOUR COST
			double mixerCost = GetLabourRate("Concrete mixer 10/7");
			double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
			double literPerDay = 30;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);

			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;

			double volPerHr = 5.66;
			double plantCostPerm3 = costPerHr / volPerHr;

			double mixingCrewduration = 4;
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalMixingCost = mixingCrewduration * mixingCrewCost;

			double finalMixing = plantCostPerm3 + totalMixingCost;

			//PLACING AND FINISHING COST
			double pokerVibratorDurationPerM3 = 1.25;
			double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
			double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

			double placingCrewPerHr = 4;
			double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

			double masonCrewPerHr = 1;
			double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
			double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

			double headmanCrewPerHr = 1;
			double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

			double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

			double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="5%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantCostPerm3 },
				new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },
			
				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
				new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 10,
				Description = "Concrete (1:4:5) grade 35 in suspended slab or wall.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem11()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/4\" diameter (350 pieces) - 6mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll")/25;
			double bindingQtyPerTon = 10;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel+ concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 48;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 48;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 5.4;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 24;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Mild Steel: 6mm steel reinforcement (350 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 11,
				Description = "Procure and place 6mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem12()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (112 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double bindingQtyPerTon = 10;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 44;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 44;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 4.8;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 22;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Mild Steel: 10-12mm steel reinforcement (180 - 112 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 12,
				Description = "Procure and place 10 to 12mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem13()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 48;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 33;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 4.8;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 16.5;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 13,
				Description = "Procure and place 10 to 12mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem14()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 33;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 33;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 6.3;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 16.5;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 14,
				Description = "Procure and place 7 to 12mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem15()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 48;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 33;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 6.3;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 16.5;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 15,
				Description = "Procure and place 7 to 12mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem16()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("5/8\" diameter (52 pieces) - 16mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 24;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 24;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 4.2;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 12;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 16 - 18mm steel reinforcement (52 - 33 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 16,
				Description = "Procure and place 16 to 18mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem17()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 39;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 39;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 4.2;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 19.5;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 17,
				Description = "Procure and place 12 to 18mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not not exceeding 3.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem18()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 24;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 24;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 5.4;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 12;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 18,
				Description = "Procure and place 13 to 19mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem19()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 39;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 39;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 5.4;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 19.5;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 19,
				Description = "Procure and place 13 to 19mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}

		private ConcreteworkItem ComputeItem20()
		{
			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wastePer = 10;
			double rebarWaste = rebarPrice * (wastePer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalMaterialCost = totalMaterialCost;

			//LABOUR COST
			double steelFixingDurationPerTon = 39;
			double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

			double labourDurationPerTon = 39;
			double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

			double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

			double steelHoistingDurationPerTon = 5.4;
			double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

			double steelHeadmanDurationPerTon = 19.5;
			double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

			double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=1, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=1, Unit="10%", TotalPrice=rebarWaste },
				new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
				new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
				new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },
			
				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 19,
				Description = "Procure and place 13 to 19mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
				Unit = "tonne",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}

		private ConcreteworkItem ComputeItem21()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem22()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem23()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem24()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem25()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem26()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem27()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem28()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem29()
		{
			throw new NotImplementedException();
		}

		private ConcreteworkItem ComputeItem30()
		{
			throw new NotImplementedException();
		}

	}
}
