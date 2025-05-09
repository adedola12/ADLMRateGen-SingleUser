using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;



namespace ADLMRateGen.ViewModel.BlockWork
{
    public class BlockworkViewModel: ViewModelBase
    {
        private readonly GetItemsFromDB _helper;
		private readonly ConcreteViewModel _concreteViewModel;

        private double _overheadPercent = 10.0;
		private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;
		// ─── Sorting / filtering helpers ──────────────────────────────────────────────
		private bool _isNetCostFilterOn = false;          // toggled by “Filter ⌄”
		private SortState _currentSort = SortState.None;  // cycles in “Sort by ⌄”

		private enum SortState { None, Overhead, TotalCost }

		public double OverheadPercent
        {
            get => _overheadPercent;
            set
            {
                if(_overheadPercent != value)
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
		public ICommand FilterCommand { get; }   // NEW
		public ICommand SortCommand { get; }   // NEW
		public ICommand AddCustomRateCommand { get; }           // ❶ NEW
		public BlockworkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib, ConcreteViewModel concreteViewModel)
		{
			_helper = new GetItemsFromDB(matLib, labourLib);
			_concreteViewModel = concreteViewModel;
            matLib.LibraryChanged += OnLibraryChange;
            labourLib.LibraryChanged += OnLibraryChange;

            BuildBlockWorkItem();

			BlockworkCollectionView = CollectionViewSource.GetDefaultView(BlockworkItems);
			BlockworkCollectionView.Filter = FilterBlockWorkItem;

			RecomputeCommand = new DelegateCommand(o => RecomputeAll());
			ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
			FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
			SortCommand = new DelegateCommand(_ => CycleSort());

			AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

			CurrencyService.Instance.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
					RecomputeAll();                 // already clears & rebuilds everything
			};
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

		// ────── FILTER – order by Net Cost (low → high) ──────
		private void ToggleNetCostFilter()
		{
			_isNetCostFilterOn = !_isNetCostFilterOn;

			BlockworkCollectionView.SortDescriptions.Clear();

			if (_isNetCostFilterOn)
				BlockworkCollectionView.SortDescriptions.Add(
					new SortDescription(nameof(BlockworkItem.NetCost),
										ListSortDirection.Ascending));
		}

		// ────── SORT – cycle → None ▪ Overhead ▪ Total Cost ──────
		private void CycleSort()
		{
			// next state
			_currentSort = _currentSort switch
			{
				SortState.None => SortState.Overhead,
				SortState.Overhead => SortState.TotalCost,
				SortState.TotalCost => SortState.None,
				_ => SortState.None
			};

			BlockworkCollectionView.SortDescriptions.Clear();

			switch (_currentSort)
			{
				case SortState.Overhead:
					BlockworkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(BlockworkItem.OverheadValue),
											ListSortDirection.Ascending));
					break;

				case SortState.TotalCost:
					BlockworkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(BlockworkItem.TotalCost),
											ListSortDirection.Ascending));
					break;

				case SortState.None:
				default:
					// back to the order in the underlying ObservableCollection
					break;
			}
		}

		private void OpenCustomRateEntry()
		{
			// create the entry view + its view‑model (DI / service‑locator would
			// be nicer, but a direct new‑up works fine here)
			var view = new CustomRateEntryView();
			view.DataContext = new CustomRateEntryViewModel();

			/* optional: close the popup when the entry VM tells us it's done
			   (expose bool IsSaved / event Saved in the entry‑VM if you like) */
			// ((CustomRateEntryViewModel)view.DataContext).Saved += () => SelectedDetail = null;

			SelectedDetail = view;         // GroundWorkView listens to this
		}
		private void BuildBlockWorkItem()
		{
			Func<BlockworkItem>[] computeMethods =
			{
				ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,
				ComputeItem7,ComputeItem8,ComputeItem9,ComputeItem10,ComputeItem11, ComputeItem12,ComputeItem13,
				ComputeItem14,ComputeItem15,ComputeItem16,ComputeItem17,ComputeItem18
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
		public double GetNetValue(Func<BlockworkItem> computeItemFunc)
		{
			var item = computeItemFunc();
			return item.NetCost;
		}
		public double GetConcreteNetValue(Func<ConcreteworkItem> computeFunc)
		{
			return _concreteViewModel.GetConcreteNetValue(computeFunc);
		}
		public double GetBlockworkNetValue(Func<ConcreteworkItem> computeFunc)
		{
			return computeFunc().NetCost;
		}
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
		public BlockworkItem ComputeItem2()
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
		public BlockworkItem ComputeItem3()
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
		public BlockworkItem ComputeItem4()
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
		public BlockworkItem ComputeItem5()
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
		private BlockworkItem ComputeItem7()
		{
			//MATERIAL COST
			double blockPrice = GetMaterialPrice("225 x 225 x 450mm (9 x 9 x 18\") Hollow blocks");
			double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

			double blockPerM2 = 10;
			double wastePer =10;

			double blockCost = blockPrice * blockPerM2;
			double blockLoadingCost = blockLoadingPrice * blockPerM2;

			double totalMaterialCost = blockCost  ;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste+ blockLoadingCost;

			double mortarCost = GetNetValue(ComputeItem4);
			double mortarQty = 0.013;
			double totalMortarCost = mortarCost * mortarQty;
			double mortarWastePer = 5;
			double mortarWaste = totalMortarCost * (mortarWastePer / 100);
			double finalCost = totalMortarCost + mortarWaste;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 140;
			double areaPerDay = outputPerDay / blockPerM2;
			double labourCostPerM2 = totalLabourCost/areaPerDay;

			double netCostPerm2 = finalMaterialCost+finalCost+labourCostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
				new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Output 140 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 7,
				Description = "225mm blockwall in cement and sand mortar (1:6)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem8()
		{
			double concreteFillingQty = 0.1030;
			double concreteCost = GetConcreteNetValue(_concreteViewModel.ComputeItem3);
			double concreteTotal = concreteFillingQty * concreteCost;

			double wastePer = 2.5;
			double waste = concreteTotal * (wastePer / 100);
			double finalMaterialCost = concreteTotal + waste;

			//LABOUR COST
			double labourCost = (GetLabourRate("Labourer")/8) * 1.4;
			double labourDuration = 0.5;
			double labourPrice = labourCost * labourDuration;

			double netFillingCost = finalMaterialCost + labourPrice;
			double blockCost = GetNetValue(ComputeItem7);

			double netCostPerm2 = netFillingCost + blockCost;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Concrete filling in 225mm blockwall", Quantity=concreteFillingQty, Unit="m3/m2", UnitPrice=concreteCost, TotalPrice=concreteTotal },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Labour filling 225mm blockwall", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourPrice },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Filling Cost", Quantity=1, Unit="", TotalPrice=netFillingCost },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 8,
				Description = "Concrete filling in 225mm blockwall",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};

		}
		private BlockworkItem ComputeItem9()
		{
			//MATERIAL COST
			double blockPrice = GetMaterialPrice("150 x 225 x 450mm (6 x 9 x 18\") Hollow blocks");
			double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

			double blockPerM2 = 10;
			double wastePer = 10;

			double blockCost = blockPrice * blockPerM2;
			double blockLoadingCost = blockLoadingPrice * blockPerM2;

			double totalMaterialCost = blockCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

			double mortarCost = GetNetValue(ComputeItem4);
			double mortarQty = 0.0084;
			double totalMortarCost = mortarCost * mortarQty;
			double mortarWastePer = 5;
			double mortarWaste = totalMortarCost * (mortarWastePer / 100);
			double finalCost = totalMortarCost + mortarWaste;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 160;
			double areaPerDay = outputPerDay / blockPerM2;
			double labourCostPerM2 = totalLabourCost / areaPerDay;

			double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
				new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 9,
				Description = "150mm blockwall in cement and sand mortar (1:6)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem10()
		{
			double concreteFillingQty = 0.0618;
			double concreteCost = GetConcreteNetValue(_concreteViewModel.ComputeItem3);
			double concreteTotal = concreteFillingQty * concreteCost;

			double wastePer = 2.5;
			double waste = concreteTotal * (wastePer / 100);
			double finalMaterialCost = concreteTotal + waste;

			//LABOUR COST
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double labourDuration = 0.33;
			double labourPrice = labourCost * labourDuration;

			double netFillingCost = finalMaterialCost + labourPrice;
			double blockCost = GetNetValue(ComputeItem7);

			double netCostPerm2 = netFillingCost + blockCost;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Concrete filling in 150mm blockwall", Quantity=concreteFillingQty, Unit="m3/m2", UnitPrice=concreteCost, TotalPrice=concreteTotal },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Labour filling 150mm blockwall", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourPrice },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Filling Cost", Quantity=1, Unit="", TotalPrice=netFillingCost },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 10,
				Description = "Concrete filling in 150mm blockwall",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem11()
		{
			//MATERIAL COST
			double blockPrice = GetMaterialPrice("100 x 225 x 450mm (4 x 9 x 18\") Hollow blocks");
			double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

			double blockPerM2 = 10;
			double wastePer = 10;

			double blockCost = blockPrice * blockPerM2;
			double blockLoadingCost = blockLoadingPrice * blockPerM2;

			double totalMaterialCost = blockCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

			double mortarCost = GetNetValue(ComputeItem4);
			double mortarQty = 0.0058;
			double totalMortarCost = mortarCost * mortarQty;
			double mortarWastePer = 5;
			double mortarWaste = totalMortarCost * (mortarWastePer / 100);
			double finalCost = totalMortarCost + mortarWaste;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 200;
			double areaPerDay = outputPerDay / blockPerM2;
			double labourCostPerM2 = totalLabourCost / areaPerDay;

			double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
				new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 11,
				Description = "100mm blockwall in cement and sand mortar (1:6)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem12()
		{
			double concreteFillingQty = 0.0442;
			double concreteCost = GetConcreteNetValue(_concreteViewModel.ComputeItem3);
			double concreteTotal = concreteFillingQty * concreteCost;

			double wastePer = 2.5;
			double waste = concreteTotal * (wastePer / 100);
			double finalMaterialCost = concreteTotal + waste;

			//LABOUR COST
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
			double labourDuration = 0.25;
			double labourPrice = labourCost * labourDuration;

			double netFillingCost = finalMaterialCost + labourPrice;
			double blockCost = GetNetValue(ComputeItem7);

			double netCostPerm2 = netFillingCost + blockCost;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Concrete filling in 100mm blockwall", Quantity=concreteFillingQty, Unit="m3/m2", UnitPrice=concreteCost, TotalPrice=concreteTotal },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Labour filling 100mm blockwall", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourPrice },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Filling Cost", Quantity=1, Unit="", TotalPrice=netFillingCost },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 12,
				Description = "Concrete filling in 100mm blockwall",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem13()
		{
			//MATERIAL COST
			double blockPrice = GetMaterialPrice("225 x 225 x 450mm (9 x 9 x 18\") Hollow blocks");
			double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

			double blockPerM2 = 10;
			double wastePer = 10;

			double blockCost = blockPrice * blockPerM2;
			double blockLoadingCost = blockLoadingPrice * blockPerM2;

			double totalMaterialCost = blockCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

			double mortarCost = GetNetValue(ComputeItem3);
			double mortarQty = 0.013;
			double totalMortarCost = mortarCost * mortarQty;
			double mortarWastePer = 5;
			double mortarWaste = totalMortarCost * (mortarWastePer / 100);
			double finalCost = totalMortarCost + mortarWaste;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 140;
			double areaPerDay = outputPerDay / blockPerM2;
			double labourCostPerM2 = totalLabourCost / areaPerDay;

			double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
				new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Output 140 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 13,
				Description = "225mm blockwall in cement and sand mortar (1:4)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem14()
		{
			//MATERIAL COST
			double blockPrice = GetMaterialPrice("150 x 225 x 450mm (6 x 9 x 18\") Hollow blocks");
			double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

			double blockPerM2 = 10;
			double wastePer = 10;

			double blockCost = blockPrice * blockPerM2;
			double blockLoadingCost = blockLoadingPrice * blockPerM2;

			double totalMaterialCost = blockCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

			double mortarCost = GetNetValue(ComputeItem3);
			double mortarQty = 0.0084;
			double totalMortarCost = mortarCost * mortarQty;
			double mortarWastePer = 5;
			double mortarWaste = totalMortarCost * (mortarWastePer / 100);
			double finalCost = totalMortarCost + mortarWaste;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 160;
			double areaPerDay = outputPerDay / blockPerM2;
			double labourCostPerM2 = totalLabourCost / areaPerDay;

			double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
				new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 14,
				Description = "150mm blockwall in cement and sand mortar (1:4)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem15()
		{
			//MATERIAL COST
			double blockPrice = GetMaterialPrice("100 x 225 x 450mm (4 x 9 x 18\") Hollow blocks");
			double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

			double blockPerM2 = 10;
			double wastePer = 10;

			double blockCost = blockPrice * blockPerM2;
			double blockLoadingCost = blockLoadingPrice * blockPerM2;

			double totalMaterialCost = blockCost;
			double waste = totalMaterialCost * (wastePer / 100);
			double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

			double mortarCost = GetNetValue(ComputeItem3);
			double mortarQty = 0.0058;
			double totalMortarCost = mortarCost * mortarQty;
			double mortarWastePer = 5;
			double mortarWaste = totalMortarCost * (mortarWastePer / 100);
			double finalCost = totalMortarCost + mortarWaste;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 200;
			double areaPerDay = outputPerDay / blockPerM2;
			double labourCostPerM2 = totalLabourCost / areaPerDay;

			double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
				new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

				new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 15,
				Description = "100mm blockwall in cement and sand mortar (1:4)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem16()
		{
			//MATERIAL COST
			double plywoodPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
			double hardwoodPrice = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
			double solignumPrice = GetMaterialPrice("Solignum (normal)")/20;

			double panelPer100m2 = 35;
			double hardwoodPer100m2 = 130;
			double solignumPer100m2 = 50;

			double panelCost = plywoodPrice * panelPer100m2;
			double hardwoodCost = hardwoodPer100m2 * hardwoodPrice;
			double solignumCost = solignumPrice * solignumPer100m2;
			double nailPer = 2.5;
			double nails = (panelCost+hardwoodCost) * (nailPer / 100);
			double wastePer = 5;
			double waste = (panelCost + hardwoodCost+solignumCost+nails) * (wastePer / 100);

			double totalMaterial = panelCost + hardwoodCost + solignumCost + nails + waste;
			double materialPerM2 = totalMaterial / 100;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 30;
			double labourPerM2 = totalLabourCost / outputPerDay;

			double netCostPerm2 = materialPerM2 + labourPerM2 ;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="18mm plywood", Quantity=panelPer100m2, Unit="No", UnitPrice=plywoodPrice, TotalPrice=panelCost },
				new BlockworkBreakdownLine { ComponentName="50 x 50mm hardwood", Quantity=hardwoodPer100m2, Unit="No", UnitPrice=hardwoodPrice, TotalPrice=hardwoodCost },
				new BlockworkBreakdownLine { ComponentName="Solignum", Quantity=solignumPer100m2, Unit="No", UnitPrice=solignumPrice, TotalPrice=solignumCost },
				new BlockworkBreakdownLine { ComponentName="Add for nail.", Quantity=nailPer, Unit="%", TotalPrice=nails },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material per 100m2", Quantity=1, Unit="", TotalPrice=totalMaterial },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material per m2", Quantity=1, Unit="", TotalPrice=materialPerM2 },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Output 30m2 per day", Quantity=outputPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 16,
				Description = "Single face timber paneling to wall comprising 18mm plywood, and 50 x 50mm timber framing at 600mm centers and including treating with solignum (Panel area to be 100m2)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem17()
		{
			//MATERIAL COST
			double plywoodPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
			double hardwoodPrice = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
			double solignumPrice = GetMaterialPrice("Solignum (normal)") / 20;

			double panelPer100m2 = 35;
			double hardwoodPer100m2 = 80;
			double solignumPer100m2 = 50;

			double panelCost = plywoodPrice * panelPer100m2;
			double hardwoodCost = hardwoodPer100m2 * hardwoodPrice;
			double solignumCost = solignumPrice * solignumPer100m2;
			double nailPer = 2.5;
			double nails = (panelCost + hardwoodCost) * (nailPer / 100);
			double wastePer = 5;
			double waste = (panelCost + hardwoodCost + solignumCost + nails) * (wastePer / 100);

			double totalMaterial = panelCost + hardwoodCost + solignumCost + nails + waste;
			double materialPerM2 = totalMaterial / 100;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 40;
			double labourPerM2 = totalLabourCost / outputPerDay;

			double netCostPerm2 = materialPerM2 + labourPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="18mm plywood", Quantity=panelPer100m2, Unit="No", UnitPrice=plywoodPrice, TotalPrice=panelCost },
				new BlockworkBreakdownLine { ComponentName="50 x 50mm hardwood", Quantity=hardwoodPer100m2, Unit="No", UnitPrice=hardwoodPrice, TotalPrice=hardwoodCost },
				new BlockworkBreakdownLine { ComponentName="Solignum", Quantity=solignumPer100m2, Unit="No", UnitPrice=solignumPrice, TotalPrice=solignumCost },
				new BlockworkBreakdownLine { ComponentName="Add for nail.", Quantity=nailPer, Unit="%", TotalPrice=nails },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material per 100m2", Quantity=1, Unit="", TotalPrice=totalMaterial },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material per m2", Quantity=1, Unit="", TotalPrice=materialPerM2 },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Output 30m2 per day", Quantity=outputPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 17,
				Description = "Single face timber paneling to wall comprising 18mm plywood, and 50 x 50mm timber framing at 1200mm centers and including treating with solignum (Panel area to be 100m2)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}
		private BlockworkItem ComputeItem18()
		{
			//MATERIAL COST
			double plywoodPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
			double hardwoodPrice = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
			double solignumPrice = GetMaterialPrice("Solignum (normal)") / 20;

			double panelPer100m2 = 70;
			double hardwoodPer100m2 = 80;
			double solignumPer100m2 = 50;

			double panelCost = plywoodPrice * panelPer100m2;
			double hardwoodCost = hardwoodPer100m2 * hardwoodPrice;
			double solignumCost = solignumPrice * solignumPer100m2;
			double nailPer = 2.5;
			double nails = (panelCost + hardwoodCost) * (nailPer / 100);
			double wastePer = 5;
			double waste = (panelCost + hardwoodCost + solignumCost + nails) * (wastePer / 100);

			double totalMaterial = panelCost + hardwoodCost + solignumCost + nails + waste;
			double materialPerM2 = totalMaterial / 100;

			//LABOUR COST
			double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
			double labourCost = GetLabourRate("Labourer") * 1.4;

			double masonQty = 3;
			double labourQty = 2;

			double masonPrice = masonCost * masonQty;
			double labourPrice = labourCost * labourQty;
			double totalLabourCost = masonPrice + labourPrice;

			double outputPerDay = 25;
			double labourPerM2 = totalLabourCost / outputPerDay;

			double netCostPerm2 = materialPerM2 + labourPerM2;

			var ohp = ApplyOHP(netCostPerm2);

			var breakdown = new ObservableCollection<BlockworkBreakdownLine>
			{
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="18mm plywood", Quantity=panelPer100m2, Unit="No", UnitPrice=plywoodPrice, TotalPrice=panelCost },
				new BlockworkBreakdownLine { ComponentName="50 x 50mm hardwood", Quantity=hardwoodPer100m2, Unit="No", UnitPrice=hardwoodPrice, TotalPrice=hardwoodCost },
				new BlockworkBreakdownLine { ComponentName="Solignum", Quantity=solignumPer100m2, Unit="No", UnitPrice=solignumPrice, TotalPrice=solignumCost },
				new BlockworkBreakdownLine { ComponentName="Add for nail.", Quantity=nailPer, Unit="%", TotalPrice=nails },
				new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material per 100m2", Quantity=1, Unit="", TotalPrice=totalMaterial },
				new BlockworkBreakdownLine { ComponentName="Sub-total: Material per m2", Quantity=1, Unit="", TotalPrice=materialPerM2 },

				new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
				new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
				new BlockworkBreakdownLine { ComponentName="Output 30m2 per day", Quantity=outputPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourPerM2 },

				new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
			};

			return new BlockworkItem
			{
				ItemNo = 18,
				Description = "Double face timber paneling to wall comprising 18mm plywood, and 50 x 50mm timber framing at 1200mm centers and including treating with solignum (Panel area to be 100m2)",
				Unit = "m2",
				NetCost = Math.Round(netCostPerm2, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				BlockworkBreakdownLine = breakdown
			};
		}

		#endregion

	}
}
