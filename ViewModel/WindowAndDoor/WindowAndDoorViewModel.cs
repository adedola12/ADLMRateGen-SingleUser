using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Groundwork;


namespace ADLMRateGen.ViewModel.WindowAndDoor
{
    public class WindowAndDoorViewModel: ViewModelBase
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
		public ObservableCollection<WindowAndDoorItem> WindowAndDoorItems { get; set; } =
			new ObservableCollection<WindowAndDoorItem>();
		public ICollectionView WindowAndDoorCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					WindowAndDoorCollectionView.Refresh();
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

		public WindowAndDoorViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourlib)
		{
			_helper = new GetItemsFromDB(matLib, labourlib);

			matLib.LibraryChanged += OnLibraryChange;
			labourlib.LibraryChanged += OnLibraryChange;

			BuildWindowAndDoorItem();

			WindowAndDoorCollectionView = CollectionViewSource.GetDefaultView(WindowAndDoorItems);
			WindowAndDoorCollectionView.Filter = FilterWindowAndDoorItem;

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
		private void OnLibraryChange()
		{
			RecomputeAll();
		}
		private bool FilterWindowAndDoorItem(object obj)
		{
			if(obj is WindowAndDoorItem item)
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
			WindowAndDoorItems.Clear();
			BuildWindowAndDoorItem();
		}
		private void ShowDetails(object o)
		{
			if(o is WindowAndDoorItem item)
			{
				var detailedControl = new WindowAndDoorDetailControl();
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

			WindowAndDoorCollectionView.SortDescriptions.Clear();

			if (_isNetCostFilterOn)
				WindowAndDoorCollectionView.SortDescriptions.Add(
					new SortDescription(nameof(WindowAndDoorItem.NetCost),
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

			WindowAndDoorCollectionView.SortDescriptions.Clear();

			switch (_currentSort)
			{
				case SortState.Overhead:
					WindowAndDoorCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(WindowAndDoorItem.OverheadValue),
											ListSortDirection.Ascending));
					break;

				case SortState.TotalCost:
					WindowAndDoorCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(WindowAndDoorItem.TotalCost),
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
		public double GetNetValue(Func<WindowAndDoorItem> computeItemFunc)
		{
			var item = computeItemFunc();
			return item.NetCost;
		}
		private void BuildWindowAndDoorItem()
		{
			Func<WindowAndDoorItem>[] computeMethods =
			{
				ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,ComputeItem7,
				ComputeItem8,
				ComputeItem9,ComputeItem10,ComputeItem11,ComputeItem12

			};

			foreach(var compute in computeMethods)
			{
				WindowAndDoorItems.Add(compute());
			}
		}
		#endregion

		#region Compute Method

		private WindowAndDoorItem ComputeItem1()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Window size 1800 x 1200mm high");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow= 1.5;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Window size 1800 x 1200mm high.", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 1,
				Description = "Supply and install natural anodised sliding window size 1800 x 1200mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};

		}
		private WindowAndDoorItem ComputeItem2()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Window size 1200 x 1200mm high");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow = 1.5;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Window size 1200 x 1200mm high.", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 2,
				Description = "Supply and install natural anodised sliding window size 1200 x 1200mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem3()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Window size 2400 x 1200mm high");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow = 2.25;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Window size 2400 x 1200mm high.", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 3,
				Description = "Supply and install natural anodised sliding window size 2400 x 1200mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem4()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Window size 900 x 600mm high");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow = 0.75;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Window size 900 x 600mm high.", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 4,
				Description = "Supply and install natural anodised sliding window size 600 x 900mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem5()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Single swing door size 900 x 2100mm high (Clear Sheet Glazing)");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow = 1.5;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Single swing door size 900 x 2100mm high (Clear Sheet Glazing)", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 5,
				Description = "Supply and install natural anodised clear sheet glazed, sinlge swing entrance door size 900 x 2100mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem6()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Double leaf single swing door size 1500 x 2100mm high (Clear Sheet Glazing)");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow = 1.65;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Double leaf single swing door size 1500 x 2100mm high (Clear Sheet Glazing)", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 6,
				Description = "Supply and install natural anodised clear sheet glazed, double swing entrance door size 1500 x 2100mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem7()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Double leaf single swing door size 1800 x 2100mm high (Clear Sheet Glazing)");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

			double glazierLabourQty = 2;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;

			double outputPerWindow = 1.75;

			double labourPerwindow = glazierLabourRate * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Double leaf single swing door size 1800 x 2100mm high (Clear Sheet Glazing)", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 7,
				Description = "Supply and install natural anodised clear sheet glazed, double swing entrance door size 1800 x 2100mm - GMP",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem8()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("44mm Solid core flush door (900 x 2100mm)");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

			double glazierLabourQty = 1;
			double labourQty = 1;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;
			double labourRate = labourCost * labourQty;

			double totalLabourCost = glazierLabourRate + labourRate;

			double outputPerWindow = 0.83;

			double labourPerwindow = totalLabourCost * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="44mm Double faced paneled door (900 x 2100mm)", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
					TotalPrice=labourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=totalLabourCost,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 8,
				Description = "Supply and install 44mm Timber flush door size 900 x 2100mm high.",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem9()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Door (750 x 2100mm)");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

			double glazierLabourQty = 1;
			double labourQty = 1;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;
			double labourRate = labourCost * labourQty;

			double totalLabourCost = glazierLabourRate + labourRate;

			double outputPerWindow = 0.83;

			double labourPerwindow = totalLabourCost * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Ditto size (750 x 2100mm)", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
					TotalPrice=labourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=totalLabourCost,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 9,
				Description = "Supply and install 44mm Timber flush door size 750 x 2100mm high.",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem10()
		{
			//MATERIAL COST
			double frameCost = GetMaterialPrice("2x4\"x12' (50x100x4200mm)");
			double solignumCost = GetMaterialPrice("Solignum (normal)") / 12;


			double frameQty = 1;
			double solignumQty = 0.05 * 0.225 * 3.6;

			double transportPer = 5;

			double frameRate = frameCost * frameQty;
			double smoothingCost = solignumCost * .3;
			double frameTransport = frameRate * (transportPer / 100);
			double solignumRate = solignumCost * solignumQty;

			double woodLength = 3.6;

			double totalMaterialCost = frameRate+smoothingCost+frameTransport+solignumRate;
			double lengthPerM = totalMaterialCost / woodLength;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

			double glazierLabourQty = 1;
			double labourQty = 1;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;
			double labourRate = labourCost * labourQty;

			double totalLabourCost = glazierLabourRate + labourRate;

			double outputPerWindow = 0.76;

			double labourPerwindow = (totalLabourCost * outputPerWindow) / woodLength;

			double netCostPerWindow = lengthPerM + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Frame size 50 x 225 x 3600mm", Quantity=frameQty, Unit="length",
					UnitPrice= frameCost, TotalPrice=frameRate},
				new WindowAndDoorBreakdownLine{ComponentName="Allow for planing smooth", TotalPrice=smoothingCost},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=frameTransport},
				new WindowAndDoorBreakdownLine{ ComponentName="Allow for treating with solignum", Quantity=solignumQty, Unit="m2",
					UnitPrice= solignumCost, TotalPrice=solignumRate},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ ComponentName="Cost per m of frame", Quantity=woodLength, Unit="m",
					UnitPrice= totalMaterialCost, TotalPrice=lengthPerM},

				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
					TotalPrice=labourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=totalLabourCost,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 10,
				Description = "Supply and install Door Frame size 50 x 225mm complete.",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem11()
		{
			//MATERIAL COST
			double frameCost = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
			double solignumCost = GetMaterialPrice("Solignum (normal)") / 12;


			double frameQty = 1;
			double solignumQty = 0.05 * 0.075 * 3.6;

			double transportPer = 5;

			double frameRate = frameCost * frameQty;
			double smoothingCost = solignumCost * .3;
			double frameTransport = frameRate * (transportPer / 100);
			double solignumRate = solignumCost * solignumQty;

			double woodLength = 3.6;

			double totalMaterialCost = frameRate + smoothingCost + frameTransport + solignumRate;
			double lengthPerM = totalMaterialCost / woodLength;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

			double glazierLabourQty = 1;
			double labourQty = 1;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;
			double labourRate = labourCost * labourQty;

			double totalLabourCost = glazierLabourRate + labourRate;

			double outputPerWindow = 0.6;

			double labourPerwindow = (totalLabourCost * outputPerWindow)/ woodLength;

			double netCostPerWindow = lengthPerM + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Frame size 50 x 225 x 3600mm", Quantity=frameQty, Unit="length",
					UnitPrice= frameCost, TotalPrice=frameRate},
				new WindowAndDoorBreakdownLine{ComponentName="Allow for planing smooth", TotalPrice=smoothingCost},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%",
					TotalPrice=frameTransport},
				new WindowAndDoorBreakdownLine{ ComponentName="Allow for treating with solignum", Quantity=solignumQty, Unit="m2",
					UnitPrice= solignumCost, TotalPrice=solignumRate},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ ComponentName="Cost per m of frame", Quantity=woodLength, Unit="m",
					UnitPrice= totalMaterialCost, TotalPrice=lengthPerM},

				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
					TotalPrice=labourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=totalLabourCost,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 11,
				Description = "Supply and install Architrave size 50 x 75mm complete.",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		private WindowAndDoorItem ComputeItem12()
		{
			//MATERIAL COST
			double windowCost = GetMaterialPrice("Mortise Lock");

			double windowQty = 1;

			double transportPer = 5;

			double windowRate = windowCost * windowQty;
			double windowTransport = windowRate * (transportPer / 100);

			double totalMaterialCost = windowRate + windowTransport;

			//LABOUR COST
			double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

			double glazierLabourQty = 1;
			double labourQty = 1;

			double glazierLabourRate = glazierLabourCost * glazierLabourQty;
			double labourRate = labourCost * labourQty;

			double totalLabourCost = glazierLabourRate + labourRate;

			double outputPerWindow = 2;

			double labourPerwindow = totalLabourCost * outputPerWindow;

			double netCostPerWindow = totalMaterialCost + labourPerwindow;

			var ohp = ApplyOHP(netCostPerWindow);

			var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
			{
				new WindowAndDoorBreakdownLine{ ComponentName="Mortise Lock", Quantity=windowQty, Unit="no",
					UnitPrice= windowCost, TotalPrice=windowRate},
				new WindowAndDoorBreakdownLine{ComponentName="Add for Screws", Quantity=transportPer, Unit="%",
					TotalPrice=windowTransport},
				new WindowAndDoorBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				new WindowAndDoorBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost,
					TotalPrice=glazierLabourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
					TotalPrice=labourRate},
				new WindowAndDoorBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
				new WindowAndDoorBreakdownLine{ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=totalLabourCost,
					TotalPrice= labourPerwindow},

				new WindowAndDoorBreakdownLine{ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
			};

			return new WindowAndDoorItem
			{
				ItemNo = 12,
				Description = "Supply and install Mortice lock",
				Unit = "No",
				NetCost = Math.Round(netCostPerWindow, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				WindowAndDoorBreakdownLines = breakdown
			};
		}
		#endregion
	}
}
