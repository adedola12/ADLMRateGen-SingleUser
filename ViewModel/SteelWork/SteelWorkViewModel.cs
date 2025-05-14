using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.Painting;

namespace ADLMRateGen.ViewModel.SteelWork
{
    public class SteelWorkViewModel: ViewModelBase
    {
		private readonly GetItemsFromDB _helper;

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
		public ObservableCollection<SteelworkItem> SteelWorkItems { get; set; } =
			new ObservableCollection<SteelworkItem>();
		public ICollectionView SteelWorkCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					SteelWorkCollectionView.Refresh();
				}
			}
		}
	
		public ICommand RecomputeCommand { get; }
		public ICommand ShowDetailsCommand { get; }
		public ICommand FilterCommand { get; }   // NEW
		public ICommand SortCommand { get; }   // NEW
		public ICommand AddCustomRateCommand { get; }           // ❶ NEW

		public SteelWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
		{
			_helper = new GetItemsFromDB(matLib, labourLib);
			matLib.LibraryChanged += OnLibraryChange;
			labourLib.LibraryChanged += OnLibraryChange;

			BuildSteelworkItem();

			SteelWorkCollectionView = CollectionViewSource.GetDefaultView(SteelWorkItems);
			SteelWorkCollectionView.Filter = FilterSteelworkItem;

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


		#region Functions
		private void OnLibraryChange()
		{
			RecomputeAll();
		}
		private bool FilterSteelworkItem(object obj)
		{
			if(obj is SteelworkItem item)
			{
				if (string.IsNullOrEmpty(SearchTerm))
				{
					return true;
				}
				return item.Description.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;

			}
			return false;

		}
		private void RecomputeAll()
		{
			SteelWorkItems.Clear();
			BuildSteelworkItem();
		}
		private void ShowDetails(object o)
		{
			if(o is SteelworkItem item)
			{
				var detailedControl = new SteelWorkDetailControl();
				detailedControl.DataContext = item;

				detailedControl.BackRequested += () =>
				{
					SelectedDetail = null;
				};
				SelectedDetail = detailedControl;
			}
		}

		// ────── FILTER – order by Net Cost (low → high) ──────
		private void ToggleNetCostFilter()
		{
			_isNetCostFilterOn = !_isNetCostFilterOn;

			SteelWorkCollectionView.SortDescriptions.Clear();

			if (_isNetCostFilterOn)
				SteelWorkCollectionView.SortDescriptions.Add(
					new SortDescription(nameof(SteelworkItem.NetCost),
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

			SteelWorkCollectionView.SortDescriptions.Clear();

			switch (_currentSort)
			{
				case SortState.Overhead:
					SteelWorkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(SteelworkItem.OverheadValue),
											ListSortDirection.Ascending));
					break;

				case SortState.TotalCost:
					SteelWorkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(SteelworkItem.TotalCost),
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
		private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
		{
			double ov = netCost * (OverheadPercent / 100);
			double pv = netCost * (ProfitPercent / 100);
			double total = netCost + ov + pv;

			return (ov, pv, total);
		}
		private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
		private double GetLabourRate(string name) => _helper.GetLabourRate(name);
		public double GetNetValue(Func<PaintWorkItem> computeItemFunc)
		{
			var item = computeItemFunc();
			return item.NetCost;
		}
		public double GetSteelNetValue(Func<SteelworkItem> computeFunc)
		{
			return computeFunc().NetCost;
		}
		#endregion

		#region Compute Item
		private void BuildSteelworkItem()
		{
			Func<SteelworkItem>[] computeMethods =
			{
				ComputeItem1,ComputeItem2,
				ComputeItem3,
				//ComputeItem4,ComputeItem5,
				//ComputeItem6,ComputeItem7,ComputeItem8,ComputeItem9,
			};

			foreach(var compute in computeMethods)
			{
				SteelWorkItems.Add(compute());
			}
		}

		public SteelworkItem ComputeItem1()
		{
			double brushCost = GetLabourRate("Power Brush");
			double brushQty = 1;
			double brushTotal = brushCost * brushQty;

			double brushPer = 10;
			double brushOv = brushTotal * (brushPer / 100);

			double artisanCost = GetLabourRate("Semi skilled") * 1.4;
			double artisanQty = 1;
			double artisanTotal = artisanCost * artisanQty;

			double totalCostPerDay = brushTotal + brushOv + artisanTotal;
			double dailyOutput = 30;
			double netCost = totalCostPerDay / dailyOutput;

			var ohp = ApplyOHP(netCost);

			var breakdown = new ObservableCollection<SteelWorkBreakdownLine>
			{
				new SteelWorkBreakdownLine{ComponentName = "Power Brush",Quantity = brushQty,Unit = "No/Day",UnitPrice = brushCost,TotalPrice = brushTotal},
				new SteelWorkBreakdownLine{ComponentName="Allow for Brushes",Quantity=brushPer, Unit="%",TotalPrice=brushOv},
				new SteelWorkBreakdownLine{ComponentName="Skilled/Artisan",Quantity=artisanQty,Unit="No/Day",UnitPrice=artisanCost,TotalPrice=artisanTotal},
				new SteelWorkBreakdownLine{ComponentName="Total Cost/Day",Quantity=dailyOutput,Unit="m",UnitPrice=totalCostPerDay,TotalPrice=netCost},
			};

			return new SteelworkItem
			{
				ItemNo = 1,
				Description = "Clean down steel surface to bare metal using power brush",
				Unit="m",
				NetCost = Math.Round(netCost, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				SteelWorkBreakdownLines = breakdown
			};
		}

		private SteelworkItem ComputeItem2()
		{
			//Blasting
			double compressorCost = GetLabourRate("Compressor") / 8;
			double fuelCost = GetMaterialPrice("Diesel");
			double sandPotCost = GetLabourRate("Sand Pot for sand blasting") / 8;
			double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
			double gritCost = GetMaterialPrice("Grit (for sand blasting)");

			double compressorQty = 0.025;
			double fuelQty = 45;
			double oilPer = 3;
			double sandPotQty = 0.025;
			double respiratoryQty = 0.025;
			double gritQty = 0.15;

			double compressorRate = compressorCost * compressorQty;
			double fuelRate = fuelCost * fuelQty;
			double sandPotRate = sandPotCost * sandPotQty;
			double respiratoryRate = respiratoryCost * respiratoryQty;
			double gritRate = gritCost * gritQty;
			double oilRate = fuelRate * (oilPer / 100);

			double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
			double blastingLabourCost = GetLabourRate("Labourer") * 1.4;
			double blastingForemanCost = GetLabourRate("Foreman") * 1.4;

			double blastingOperatorQty = 1;
			double blastingLabouurQty = 3;
			double blastingForemanQty = 1;

			double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
			double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
			double blastingForemanRate = blastingForemanCost * blastingForemanQty;

			double blastingLabour = blastingOperatorRate + blastingLabourRate+blastingForemanRate;
			double blastingOutputDaily = 300;

			double blastingPerSqm = blastingLabour / blastingOutputDaily;

			double netCost = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

			var ohp = ApplyOHP(netCost);

			var breakdown = new ObservableCollection<SteelWorkBreakdownLine>
			{
				new SteelWorkBreakdownLine{ ComponentName="Compressor", Quantity=compressorQty, Unit="hr/m2",
					UnitPrice= compressorCost, TotalPrice=compressorRate},
				new SteelWorkBreakdownLine{ ComponentName="Fuel (Diesel)", Quantity=fuelQty, Unit="lit/day",
					UnitPrice= fuelCost, TotalPrice=fuelRate},
				new SteelWorkBreakdownLine{ComponentName="Oil and consumables (per day)", Quantity=oilPer, Unit="%",
					TotalPrice=oilRate},
				new SteelWorkBreakdownLine{ ComponentName="Sand Pot", Quantity=sandPotQty, Unit="hr/m2",
					UnitPrice= sandPotCost, TotalPrice=sandPotRate},
				new SteelWorkBreakdownLine{ ComponentName="Respiratory gear.", Quantity=respiratoryQty, Unit="hr/m2",
					UnitPrice= respiratoryCost, TotalPrice=respiratoryRate},
				new SteelWorkBreakdownLine{ ComponentName="Grit", Quantity=gritQty, Unit="m3/m2",
					UnitPrice= gritCost, TotalPrice=gritRate},

				new SteelWorkBreakdownLine{ ComponentName="Blasting operator.", Quantity=blastingOperatorQty, Unit="per/day",
					UnitPrice= blastingOperatorCost, TotalPrice=blastingOperatorRate},
				new SteelWorkBreakdownLine{ ComponentName="Labour (for loading sand pot)", Quantity=blastingLabouurQty, Unit="per/day",
					UnitPrice= blastingLabourCost, TotalPrice=blastingLabourRate},
				new SteelWorkBreakdownLine{ ComponentName="Foreman", Quantity=blastingForemanQty, Unit="per/day", UnitPrice=blastingForemanCost,
				TotalPrice=blastingForemanRate},
				new SteelWorkBreakdownLine{ComponentName="Labour Output", Quantity=blastingOutputDaily, Unit="m2/day", UnitPrice=blastingLabour,
					TotalPrice=blastingPerSqm},
				new SteelWorkBreakdownLine{ComponentName="Total Blasting ", TotalPrice=netCost},
			};

			return new SteelworkItem
			{
				ItemNo = 2,
				Description = "Prepare surface of structure by grit blasting to SP10",
				Unit = "m2",
				NetCost = Math.Round(netCost, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				SteelWorkBreakdownLines = breakdown
			};
		}

		private SteelworkItem ComputeItem3()
		{
			//Blasting
			double compressorCost = GetLabourRate("Compressor") / 8;
			double fuelCost = GetMaterialPrice("Diesel");
			double sandPotCost = GetLabourRate("Sand Pot for sand blasting") / 8;
			double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
			double gritCost = GetMaterialPrice("Sharp Sand");

			double compressorQty = 0.025;
			double fuelQty = 45;
			double oilPer = 3;
			double sandPotQty = 0.025;
			double respiratoryQty = 0.025;
			double gritQty = 0.15;

			double compressorRate = compressorCost * compressorQty;
			double fuelRate = fuelCost * fuelQty;
			double sandPotRate = sandPotCost * sandPotQty;
			double respiratoryRate = respiratoryCost * respiratoryQty;
			double gritRate = gritCost * gritQty;
			double oilRate = fuelRate * (oilPer / 100);

			double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
			double blastingLabourCost = GetLabourRate("Labourer") * 1.4;
			double blastingForemanCost = GetLabourRate("Foreman") * 1.4;

			double blastingOperatorQty = 1;
			double blastingLabouurQty = 3;
			double blastingForemanQty = 1;

			double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
			double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
			double blastingForemanRate = blastingForemanCost * blastingForemanQty;

			double blastingLabour = blastingOperatorRate + blastingLabourRate + blastingForemanRate;
			double blastingOutputDaily = 300;

			double blastingPerSqm = blastingLabour / blastingOutputDaily;

			double netCost = compressorRate + gritRate + fuelRate + sandPotRate + respiratoryRate + oilRate + blastingPerSqm;

			var ohp = ApplyOHP(netCost);

			var breakdown = new ObservableCollection<SteelWorkBreakdownLine>
			{
				new SteelWorkBreakdownLine{ ComponentName="Compressor", Quantity=compressorQty, Unit="hr/m2",
					UnitPrice= compressorCost, TotalPrice=compressorRate},
				new SteelWorkBreakdownLine{ ComponentName="Fuel (Diesel)", Quantity=fuelQty, Unit="lit/day",
					UnitPrice= fuelCost, TotalPrice=fuelRate},
				new SteelWorkBreakdownLine{ComponentName="Oil and consumables (per day)", Quantity=oilPer, Unit="%",
					TotalPrice=oilRate},
				new SteelWorkBreakdownLine{ ComponentName="Sand Pot", Quantity=sandPotQty, Unit="hr/m2",
					UnitPrice= sandPotCost, TotalPrice=sandPotRate},
				new SteelWorkBreakdownLine{ ComponentName="Respiratory gear.", Quantity=respiratoryQty, Unit="hr/m2",
					UnitPrice= respiratoryCost, TotalPrice=respiratoryRate},
				new SteelWorkBreakdownLine{ ComponentName="Grit", Quantity=gritQty, Unit="m3/m2",
					UnitPrice= gritCost, TotalPrice=gritRate},

				new SteelWorkBreakdownLine{ ComponentName="Blasting operator.", Quantity=blastingOperatorQty, Unit="per/day",
					UnitPrice= blastingOperatorCost, TotalPrice=blastingOperatorRate},
				new SteelWorkBreakdownLine{ ComponentName="Labour (for loading sand pot)", Quantity=blastingLabouurQty, Unit="per/day",
					UnitPrice= blastingLabourCost, TotalPrice=blastingLabourRate},
				new SteelWorkBreakdownLine{ ComponentName="Foreman", Quantity=blastingForemanQty, Unit="per/day", UnitPrice=blastingForemanCost,
				TotalPrice=blastingForemanRate},
				new SteelWorkBreakdownLine{ComponentName="Labour Output", Quantity=blastingOutputDaily, Unit="m2/day", UnitPrice=blastingLabour,
					TotalPrice=blastingPerSqm},
				new SteelWorkBreakdownLine{ComponentName="Total Blasting ", TotalPrice=netCost},
			};

			return new SteelworkItem
			{
				ItemNo = 3,
				Description = "Prepare surface of structure by sand blasting to SP10",
				Unit = "m2",
				NetCost = Math.Round(netCost, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 0),
				ProfitValue = Math.Round(ohp.profitVal, 0),
				TotalCost = Math.Round(ohp.total, 2),
				SteelWorkBreakdownLines = breakdown
			};
		}

		private SteelworkItem ComputeItem4()
		{
			throw new NotImplementedException();
		}

		private SteelworkItem ComputeItem5()
		{
			throw new NotImplementedException();
		}

		private SteelworkItem ComputeItem6()
		{
			throw new NotImplementedException();
		}

		private SteelworkItem ComputeItem7()
		{
			throw new NotImplementedException();
		}

		private SteelworkItem ComputeItem8()
		{
			throw new NotImplementedException();
		}

		private SteelworkItem ComputeItem9()
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
