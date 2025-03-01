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
			Func<ConcreteworkItem>[] computeMethods =
			{
				ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
				ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
				ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16, ComputeItem17, ComputeItem18,
				ComputeItem19, ComputeItem20, ComputeItem21, ComputeItem22, ComputeItem23, ComputeItem24, ComputeItem25,
				ComputeItem26, ComputeItem27, ComputeItem28, ComputeItem29, ComputeItem30, ComputeItem31
			};

			foreach (var compute in computeMethods)
			{
				ConcreteWorkItems.Add(compute());
			}

		}

		#region Helper Methods

		private double ComputePlantCost(string mixerName, double dieselMultiplier, double literPerDay, double operatorMultiplier, double volPerHr)
		{
			double mixerCost = GetLabourRate(mixerName);
			double dieselPrice = (GetLabourRate("Labourer") / 8) * dieselMultiplier;
			double fuelCost = dieselPrice * literPerDay;
			double operatorCost = GetLabourRate("Heavy plant operator") * operatorMultiplier;
			double totalPlantDay = mixerCost + fuelCost +
				(0.03 * fuelCost) + (2 * operatorCost);
			double workHr = 8;
			double costPerHr = totalPlantDay / workHr;
			return costPerHr / volPerHr;
		}

		private double ComputeMixingCost(double duration)
		{
			double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			return duration * mixingCrewCost;
		}

		private double ApplyWaste(double cost, double wastePercent)
		{
			double waste = cost * (wastePercent / 100);
			return cost + waste;
		}
		private ConcreteworkItem CreaateItem(int itemNo, string description, string unit, double netCost, ObservableCollection<ConcreteworkBreakdownLine> breakdown)
		{
			var ohp = ApplyOHP(netCost);
			return new ConcreteworkItem
			{
				ItemNo = itemNo,
				Description = description,
				Unit = unit,
				NetCost = Math.Round(netCost, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}

		#endregion
		private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
		{
			double ov = netCost * (OverheadPercent / 100);
			double pv = netCost * (ProfitPercent / 100);
			double total = netCost + ov + pv;

			return (ov, pv, total);
		}
		private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
		private double GetLabourRate(string name) => _helper.GetLabourRate(name);

		#region ComputeItem Method
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
			//double netCostPerm3 = ComputePlantCost("Concrete mixer 10/7", 1.0,30,1.4,5.66);
			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				new ConcreteworkBreakdownLine { ComponentName="Concrete 10/14 mixer.", Quantity=1, Unit="N/day", UnitPrice=GetLabourRate("Concrete mixer 10/7") },
				new ConcreteworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=30, Unit="hr/m3", UnitPrice=GetLabourRate("Labourer") / 8, TotalPrice=(GetLabourRate("Labourer")/8)*30 },
				new ConcreteworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=3, Unit="%", TotalPrice=0.03 * 0.03*((GetLabourRate("Labourer")/8)*30) },
				new ConcreteworkBreakdownLine { ComponentName="Operator (per day)", Quantity=2, Unit="Nr/Day", UnitPrice=GetLabourRate("Heavy plant operator")*1.4, TotalPrice=(GetLabourRate("Heavy plant operator")*1.4)*2 },
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
			double boardLengthPerm2 = 0.93;
			double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
			double totalBoardPrice = boardPrice * boardLengthPerm2;
			double propsAndBraceLengthPerm2 = 1.56;
			double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
			double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;
			double propsAndBrace2LengthPerm2 = 2.25;
			double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
			double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
			double totalWoodPrice = totalBoardPrice+totalpropsAndBracePrice+ totalpropsAndBrace2Price;
			double wastePerM2 = 6;
			double cuttingWaste = totalWoodPrice * (wastePerM2 / 100);
			double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

			double nailFirstUse = 0.5;
			double nailFiveUse = 0.125 * 5;
			double nailSixUse = nailFirstUse + nailFiveUse;
			double nailPrice = GetMaterialPrice("Nails 4\"")/25;
			double totalNailPrice = nailPrice * nailSixUse;

			double mouldOilSingleUse = 0.27;
			double mouldOilSixUse = mouldOilSingleUse * 6;
			double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
			double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

			double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
			double materialCostPerUse = totalMaterialCost / 6;

			//LABOUR COST
			double carpenterDurationPerFirstUse = 1.8;
			double carpenterDurationPerFiveUse = 1.3 * 5;
			double totalCarpentaDurationPer6Use = carpenterDurationPerFirstUse + carpenterDurationPerFiveUse;
			double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

			double labourDurationPerFirstUse = 1.8;
			double labourDurationPerFiveUse = 1.3 * 5;
			double totalLabourDurationPer6Use = labourDurationPerFirstUse + labourDurationPerFiveUse;
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

			double headmanDurationPerSqm = 4.15;
			double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

			double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
			double totalLabourPerUse = totalLabourCostForSixUse / 6;

			double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

			double netCostPerTon = materialCostPerUse + totalLabourPerUse;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="25 x 300mm x 3600mm boards.", Quantity=boardLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=6, Unit="%",  TotalPrice=cuttingWaste},
				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

				new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



				new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
				new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 20,
				Description = "Sawn formwork to sides and soffit of beams and lintels not exceeding 3.00mm height from ground floor level. Six (6) number maximum uses.",
				Unit = "m2",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem21()
		{
			//MATERIAL COST
			double boardLengthPerm2 = 0.93;
			double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
			double totalBoardPrice = boardPrice * boardLengthPerm2;

			double propsAndBraceLengthPerm2 = 1.56;
			double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
			double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

			double propsAndBrace2LengthPerm2 = 2.25;
			double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
			double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
			double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

			double wastePerM2 = 5;
			double cuttingWaste = totalWoodPrice * (wastePerM2 / 100);
			double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

			double nailFirstUse = 0.5;
			double nailFiveUse = 0.125 * 5;
			double nailSixUse = nailFirstUse + nailFiveUse;
			double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
			double totalNailPrice = nailPrice * nailSixUse;

			double mouldOilSingleUse = 0.27;
			double mouldOilSixUse = mouldOilSingleUse * 6;
			double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
			double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

			double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
			double materialCostPerUse = totalMaterialCost / 6;

			//LABOUR COST
			double carpenterDurationPerFirstUse = 2.07;
			double carpenterDurationPerFiveUse = 1.5 * 5;
			double totalCarpentaDurationPer6Use = carpenterDurationPerFirstUse + carpenterDurationPerFiveUse;
			double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

			double labourDurationPerFirstUse = 2.07;
			double labourDurationPerFiveUse = 1.5 * 5;
			double totalLabourDurationPer6Use = labourDurationPerFirstUse + labourDurationPerFiveUse;
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

			double headmanDurationPerSqm = 4.77;
			double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

			double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
			double totalLabourPerUse = totalLabourCostForSixUse / 6;

			double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

			double netCostPerTon = materialCostPerUse + totalLabourPerUse;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="25 x 300mm x 3600mm boards.", Quantity=boardLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=6, Unit="%",  TotalPrice=cuttingWaste},
				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

				new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



				new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
				new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 21,
				Description = "Sawn formwork to sides and soffit of beams and lintels over 3.00mm but not exceeding 6.00m height from ground floor level. Six (6) number maximum uses.",
				Unit = "m2",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem22()
		{
			//MATERIAL COST
			double boardLengthPerm2 = 0.93;
			double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
			double totalBoardPrice = boardPrice * boardLengthPerm2;

			double propsAndBraceLengthPerm2 = 1.56;
			double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
			double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

			double propsAndBrace2LengthPerm2 = 2.25;
			double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
			double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
			double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

			double wastePerM2 = 5;
			double cuttingWaste = totalWoodPrice * (wastePerM2 / 100);
			double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

			double nailFirstUse = 0.5;
			double nailFiveUse = 0.125 * 5;
			double nailSixUse = nailFirstUse + nailFiveUse;
			double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
			double totalNailPrice = nailPrice * nailSixUse;

			double mouldOilSingleUse = 0.27;
			double mouldOilSixUse = mouldOilSingleUse * 6;
			double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
			double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

			double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
			double materialCostPerUse = totalMaterialCost / 6;

			//LABOUR COST
			double carpenterDurationPerFirstUse = 1.62;
			double carpenterDurationPerFiveUse = 1.13 * 5;
			double totalCarpentaDurationPer6Use = Math.Round(carpenterDurationPerFirstUse + carpenterDurationPerFiveUse, 2);
			double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

			double labourDurationPerFirstUse = 1.62;
			double labourDurationPerFiveUse = 1.13 * 5;
			double totalLabourDurationPer6Use = Math.Round(labourDurationPerFirstUse + labourDurationPerFiveUse, 2);
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

			double headmanDurationPerSqm = 4.19;
			double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

			double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
			double totalLabourPerUse = totalLabourCostForSixUse / 6;

			double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

			double netCostPerTon = materialCostPerUse + totalLabourPerUse;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="25 x 300mm x 3600mm boards.", Quantity=boardLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=6, Unit="%",  TotalPrice=cuttingWaste},
				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

				new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



				new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
				new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 22,
				Description = "Sawn formwork to vertical sides of columns not exceeding 3.00mm height from ground floor level. Six (6) number maximum uses.",
				Unit = "m2",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem23()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 10.8;
			double sandPerM3 =0.38;
			double stonePerM3 = 0.75;
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
				ItemNo = 23,
				Description = "Concrete (1:1:2) grade 30 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem24()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 10.8;
			double sandPerM3 = 0.38;
			double stonePerM3 = 0.75;
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
				ItemNo = 24,
				Description = "Concrete (1:1:2) grade 30 in suspended slab or wall.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem25()
		{
			//MATERIAL COST
			double meshPrice = GetMaterialPrice("A142 at 2.22kg/m2 size 4.8 x 2.4m x 2.2kg/m2");
			double sheetSize = 11.52;
			double costPerSqm =meshPrice/sheetSize;
			double bindingWirePer = 5;
			double bindingWireCost = costPerSqm * (bindingWirePer / 100);
			double wastePer = 5;
			double wasteCost = (costPerSqm+ bindingWireCost) * (wastePer / 100);

			double totalMaterial = costPerSqm + bindingWireCost + wasteCost;

			//LABOUR COST
			double steelFixingLabour = 0.15;
			double steelFixerCostPerHr = ((GetLabourRate("Welder") / 8) * 1.4);
			double totalSteelFixerCost = steelFixingLabour * steelFixerCostPerHr;

			double labourDuration = 0.15;
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totallabourCost = labourDuration * labourCostPerHr;

			double totalMeshLabour = totalSteelFixerCost+totallabourCost;

			double totalcostPerM2 = totalMaterial + totalMeshLabour;

			double netCostPerm2 = totalcostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Material cost", Quantity=1, Unit="", UnitPrice=meshPrice, },
				new ConcreteworkBreakdownLine { ComponentName="Sheet size 2.4m x 4.8m wide.", Quantity=sheetSize, Unit="m2" },
				new ConcreteworkBreakdownLine { ComponentName="Mesh Cost Per M2", Quantity=1, Unit="N/m2", TotalPrice=costPerSqm },
				new ConcreteworkBreakdownLine { ComponentName="Add  for binding wire and overlaps", Quantity=bindingWirePer, Unit="%",  TotalPrice=bindingWireCost},
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%",  TotalPrice=wasteCost},
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterial },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steel Fixers", Quantity=steelFixingLabour, Unit="hr/m2", UnitPrice=steelFixerCostPerHr, TotalPrice=totalSteelFixerCost},
				new ConcreteworkBreakdownLine { ComponentName="Labour", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totallabourCost},

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour", Quantity=1, Unit="m2", TotalPrice=totalMeshLabour },
				new ConcreteworkBreakdownLine { ComponentName="Total: ", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 25,
				Description = "Procure and place BRC mesh Ref. A142 in floor slab",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem26()
		{
			//MATERIAL COST
			double meshPrice = GetMaterialPrice("A193 at 3.95kg/m2 size 4.8 x 2.4m x 3.02kg/m2");
			double sheetSize = 11.52;
			double costPerSqm = meshPrice / sheetSize;
			double bindingWirePer = 5;
			double bindingWireCost = costPerSqm * (bindingWirePer / 100);
			double wastePer = 5;
			double wasteCost = (costPerSqm + bindingWireCost) * (wastePer / 100);

			double totalMaterial = costPerSqm + bindingWireCost + wasteCost;

			//LABOUR COST
			double steelFixingLabour = 0.18;
			double steelFixerCostPerHr = ((GetLabourRate("Welder") / 8) * 1.4);
			double totalSteelFixerCost = steelFixingLabour * steelFixerCostPerHr;

			double labourDuration = 0.18;
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totallabourCost = labourDuration * labourCostPerHr;

			double totalMeshLabour = totalSteelFixerCost + totallabourCost;

			double totalcostPerM2 = totalMaterial + totalMeshLabour;

			double netCostPerm2 = totalcostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Material cost", Quantity=1, Unit="", UnitPrice=meshPrice, },
				new ConcreteworkBreakdownLine { ComponentName="Sheet size 2.4m x 4.8m wide.", Quantity=sheetSize, Unit="m2" },
				new ConcreteworkBreakdownLine { ComponentName="Mesh Cost Per M2", Quantity=1, Unit="N/m2", TotalPrice=costPerSqm },
				new ConcreteworkBreakdownLine { ComponentName="Add  for binding wire and overlaps", Quantity=bindingWirePer, Unit="%",  TotalPrice=bindingWireCost},
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%",  TotalPrice=wasteCost},
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterial },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steel Fixers", Quantity=steelFixingLabour, Unit="hr/m2", UnitPrice=steelFixerCostPerHr, TotalPrice=totalSteelFixerCost},
				new ConcreteworkBreakdownLine { ComponentName="Labour", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totallabourCost},

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour", Quantity=1, Unit="m2", TotalPrice=totalMeshLabour },
				new ConcreteworkBreakdownLine { ComponentName="Total: ", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 26,
				Description = "Procure and place BRC mesh Ref. A193 in floor slab",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem27()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 10.8;
			double sandPerM3 = 0.38;
			double stonePerM3 = 0.75;
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

			//MATERIAL COST
			double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
			double wasteRebarPer = 10;
			double rebarWaste = rebarPrice * (wasteRebarPer / 100);
			double bindingQtyPerTon = 10;
			double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
			double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
			double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
			double unloadingDurationPerTon = 3;
			double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
			double concreteSpacerPer = 5;
			double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

			double totalRebarMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
			double finalRebarMaterialCost = totalRebarMaterialCost;

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

			double netCostPerTon = finalRebarMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

			//MATERIAL COST
			double boardLengthPerm2 = 0.93;
			double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
			double totalBoardPrice = boardPrice * boardLengthPerm2;

			double propsAndBraceLengthPerm2 = 1.56;
			double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
			double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

			double propsAndBrace2LengthPerm2 = 2.25;
			double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
			double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
			double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

			double wastePerM2 = 5;
			double cuttingWaste = totalWoodPrice * (wastePerM2 / 100);
			double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

			double nailFirstUse = 0.5;
			double nailFiveUse = 0.125 * 5;
			double nailSixUse = nailFirstUse + nailFiveUse;
			double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
			double totalNailPrice = nailPrice * nailSixUse;

			double mouldOilSingleUse = 0.27;
			double mouldOilSixUse = mouldOilSingleUse * 6;
			double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
			double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

			double totalWoodMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
			double materialCostPerUse = totalWoodMaterialCost / 6;

			//LABOUR COST
			double carpenterDurationPerFirstUse = 1.62;
			double carpenterDurationPerFiveUse = 1.13 * 5;
			double totalCarpentaDurationPer6Use = Math.Round(carpenterDurationPerFirstUse + carpenterDurationPerFiveUse, 2);
			double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

			double labourDurationPerFirstUse = 1.62;
			double labourDurationPerFiveUse = 1.13 * 5;
			double totalLabourDurationPer6Use = Math.Round(labourDurationPerFirstUse + labourDurationPerFiveUse, 2);
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

			double headmanDurationPerSqm = 4.19;
			double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

			double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
			double totalLabourPerUse = totalLabourCostForSixUse / 6;

			double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

			double netWoodCostPerM2 = materialCostPerUse + totalLabourPerUse;

			//PRECAST CALCULATION
			double precastRebarQty = 0.12;
			double precastRebarCost = precastRebarQty * netCostPerTon;

			double precastWoodQty = 5;
			double precastWoodCost = precastWoodQty * netWoodCostPerM2;

			double precastTotal = netCostPerm3+ precastRebarCost + precastWoodCost;

			double mixingPreCastCrewduration = 9;
			double mixingPrecastCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPreCastMixingCost = mixingPreCastCrewduration * mixingPrecastCrewCost;

			double placingPercastCrewPerHr = 12;
			double placingPrecastCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalPlacingPrecastCrewCost = placingPercastCrewPerHr * placingPrecastCrewCost;

			double totalPrecastLabour = totalPreCastMixingCost + totalPlacingPrecastCrewCost;

			double precastNetTotal = precastTotal + totalPrecastLabour;

			double extraPreCastPer = 30;
			double extraPrecast = precastNetTotal * (extraPreCastPer / 100);
			double precastFinalTotal = precastNetTotal + extraPrecast;


			var ohp = ApplyOHP(precastFinalTotal);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Concrete grade 30", Quantity=1, Unit="m3", UnitPrice=netCostPerm3, TotalPrice=netCostPerm3},
				new ConcreteworkBreakdownLine { ComponentName="Reinforcement", Quantity=precastRebarQty, Unit="Tonne/m3", UnitPrice=netCostPerTon, TotalPrice=precastRebarCost},
				new ConcreteworkBreakdownLine { ComponentName="Formwork", Quantity=precastWoodQty, Unit="m2/m3", UnitPrice=netWoodCostPerM2, TotalPrice=precastWoodCost},
				new ConcreteworkBreakdownLine { ComponentName="Material cost", Quantity=1, Unit="", TotalPrice=precastTotal },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Mixing - as before calculated", Quantity=1, Unit="lot/m3", UnitPrice=totalPreCastMixingCost, TotalPrice=totalPreCastMixingCost},
				new ConcreteworkBreakdownLine { ComponentName="Placing - as before calculated", Quantity=1, Unit="lot/m3", UnitPrice=totalPlacingPrecastCrewCost, TotalPrice=totalPlacingPrecastCrewCost},

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour", Quantity=1, Unit="lot/m3", TotalPrice=totalPrecastLabour },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material & Labour", Quantity=1, Unit="m3", TotalPrice=precastNetTotal },
				new ConcreteworkBreakdownLine { ComponentName="Extra over costs for incidental works associated", Quantity=extraPreCastPer, Unit="%", TotalPrice=extraPrecast},

				new ConcreteworkBreakdownLine { ComponentName="Total: Cost per m3 ", Quantity=1, Unit="m3", TotalPrice=precastFinalTotal },
			};

			return new ConcreteworkItem
			{
				ItemNo = 27,
				Description = "Precast concrete grade 30 Material (1m3 Of Concrete)",
				Unit = "m2",
				NetCost = Math.Round(precastFinalTotal, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem28()
		{
			//MATERIAL COST
			double plyWoodLengthPerm2 = 0.35;
			double boardPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
			double totalBoardPrice = boardPrice * plyWoodLengthPerm2;

			double propsAndBraceLengthPerm2 = 1.56;
			double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
			double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

			double propsAndBrace2LengthPerm2 = 2.25;
			double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
			double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
			double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

			double wastePerM2 = 5;
			double cuttingWaste = totalWoodPrice * (wastePerM2 / 100);
			double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

			double nailFirstUse = 0.5;
			double nailFiveUse = 0.125 * 5;
			double nailSixUse = nailFirstUse + nailFiveUse;
			double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
			double totalNailPrice = nailPrice * nailSixUse;

			double mouldOilSingleUse = 0.27;
			double mouldOilSixUse = mouldOilSingleUse * 6;
			double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
			double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

			double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
			double materialCostPerUse = totalMaterialCost / 6;

			//LABOUR COST
			double carpenterDurationPerFirstUse = 1.62;
			double carpenterDurationPerFiveUse = 1.13 * 5;
			double totalCarpentaDurationPer6Use = Math.Round(carpenterDurationPerFirstUse + carpenterDurationPerFiveUse, 2);
			double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

			double labourDurationPerFirstUse = 1.62;
			double labourDurationPerFiveUse = 1.13 * 5;
			double totalLabourDurationPer6Use = Math.Round(labourDurationPerFirstUse + labourDurationPerFiveUse, 2);
			double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
			double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

			double headmanDurationPerSqm = 4.19;
			double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
			double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

			double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
			double totalLabourPerUse = totalLabourCostForSixUse / 6;

			double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

			double netCostPerTon = materialCostPerUse + totalLabourPerUse;

			var ohp = ApplyOHP(netCostPerTon);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="18mm plywood", Quantity=plyWoodLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
				new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
				new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=6, Unit="%",  TotalPrice=cuttingWaste},
				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
				new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

				new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

				new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
				new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },
			
				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

				new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
				new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

				new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



				new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
				new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
			};

			return new ConcreteworkItem
			{
				ItemNo = 28,
				Description = "Sawn formwork to vertical sides of columns to form fair faced concrete not exceeding 3.00mm height from ground floor level. Six (6) number maximum uses.",
				Unit = "m2",
				NetCost = Math.Round(netCostPerTon, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem29()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("EMACO S88CA (imported shrinkage compensated cement for concrete repair)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 10.8;
			double sandPerM3 = 0.38;
			double stonePerM3 = 0.75;
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
				new ConcreteworkBreakdownLine { ComponentName="Emaco S88CA Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
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
				ItemNo = 29,
				Description = "Concrete (1:1:2) grade 30 for repair works in water logged areas, utilising imported shrinkage compensated cement",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem30()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			//double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 28.82;
			double sandPerM3 = 10;
			//double stonePerM3 = 0.75;
			double wastePer = 25;

			double cementCost = cementPerM3 * cementPrice;
			double cementLoadingCost = cementLoadingPrice * cementPerM3;
			double sandCost = sandPerM3 * sandPrice;
			//double stoneCost = stonePerM3 * stonePrice;

			double totalMaterialCost = cementCost + cementLoadingCost + sandCost ;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste;

			double materialCostPerCum = finalMaterialCost/11;

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

			
			double netCostPerm3 = materialCostPerCum + netCostPerUse ;

			var ohp = ApplyOHP(netCostPerm3);

			var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
			{
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
				new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
				new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
				new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },
				

				new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
			};

			return new ConcreteworkItem
			{
				ItemNo = 30,
				Description = "Cement Soil, mix (1:10)",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		private ConcreteworkItem ComputeItem31()
		{
			//MATERIAL COST
			double cementPrice = GetMaterialPrice("Cement (50kg bag)");
			double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
			double sandPrice = GetMaterialPrice("Sharp Sand");
			double stonePrice = GetMaterialPrice("Washed gravel (local)");
			//double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

			double cementPerM3 = 24;
			double sandPerM3 = 0.21;
			double stonePerM3 = 0.43;
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
				ItemNo = 31,
				Description = "Concrete (1:1/4:1/2) grade 40 in foundation or slab.",
				Unit = "m3",
				NetCost = Math.Round(netCostPerm3, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				ConcreteBreakdownLine = breakdown
			};
		}
		
		#endregion

	}
}
