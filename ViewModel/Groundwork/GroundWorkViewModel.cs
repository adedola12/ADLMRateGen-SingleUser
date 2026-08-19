using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.Groundwork
{
	public class GroundWorkViewModel : ViewModelBase
	{
		private readonly GetItemsFromDB _helper;


		private double _overheadPercent = 10.0;
		private double _profitPercent = 25.0;
		private string _searchTerm = string.Empty;
		private object _selectedDetail;
		// ─── Sorting / filtering helpers ──────────────────────────────────────────────
		private bool _isNetCostFilterOn = false;          // toggled by “Filter ⌄”
		private SortState _currentSort = SortState.None;  // cycles in “Sort by ⌄”

        private const string SectionKey = SectionKeys.Ground;
        // ✅ Ground section key
        private readonly ComputeItemEngine _computeEngine;
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

		public ObservableCollection<GroundworkItem> GroundworkItems { get; set; }
			= new ObservableCollection<GroundworkItem>();
		public ICollectionView GroundworkCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					GroundworkCollectionView.Refresh();
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
		public GroundWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
		{
			//_materialLib = matLib;
			//_labourLib = labourLib;

			_helper = new GetItemsFromDB(matLib, labourLib);

			matLib.LibraryChanged += OnLibraryChanged;
			labourLib.LibraryChanged += OnLibraryChanged;

            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);
            _ = LoadComputeCatalogForSectionAsync();

            RateLibraryStore.Changed += OnLibraryChanged;
            _ = LoadRateLibraryAsync();


            BuildGroundWorkItems();

			GroundworkCollectionView = CollectionViewSource.GetDefaultView(GroundworkItems);
			GroundworkCollectionView.Filter = FilterGroundWorkItem;

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

			UserRateEditStore.Current.OverridesChanged += (_, __) =>
			{
				void Refresh()
				{
					var nos = GroundworkItems.Select(i => i.ItemNo).ToList();
					foreach (var n in nos) RecomputeItemInPlace(n);
				}
				var disp = System.Windows.Application.Current?.Dispatcher;
				if (disp == null || disp.CheckAccess()) Refresh();
				else disp.BeginInvoke((Action)Refresh);
			};

        }

        private async Task LoadRateLibraryAsync()
        {
            try
            {
                // Always show cached rates first (offline-friendly)
                RateLibraryStore.ReloadFromDisk();
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] Cached disk items={RateLibraryStore.Items?.Count ?? 0}");

                // pull latest (requires logged-in token)
                var ok = await RateLibraryStore.RefreshFromApiAsync(SectionKey);

                System.Diagnostics.Debug.WriteLine(
                    $"[RateLibrary] Refresh ok={ok}, status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");

                // fallback if section mismatch
                if (ok && RateLibraryStore.LastApiItemCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[RateLibrary] Section returned 0. Retrying without sectionKey...");
                    await RateLibraryStore.RefreshFromApiAsync();

                    System.Diagnostics.Debug.WriteLine(
                        $"[RateLibrary] Retry(all) status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");
                }

                // Ensure UI refresh on dispatcher
                OnLibraryChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] load failed: {ex}");
            }
        }



        private async Task LoadComputeCatalogForSectionAsync()
        {
            try
            {
                // Try server-side section filter
                var ok = await ComputeCatalogStore.RefreshFromApiAsync(SectionKey);

                // 🔁 Fallback: if API returns none for this section key, try without section filter
                // (only do this if your backend section naming is inconsistent)
                if (ok && ComputeCatalogStore.LastApiItemCount == 0)
                {
                    await ComputeCatalogStore.RefreshFromApiAsync(); // fetch all
                }

                // Rebuild to append whatever is now in ComputeCatalogStore.Items
                RecomputeAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Compute API load failed: {ex}");
                // still show cached disk items already loaded
            }
        }



        private void ShowDetails(object o)
		{
			if (o is GroundworkItem item)
			{
				var detailedControl = new GroundworkItemDetailControl();
				detailedControl.DataContext = item;

				detailedControl.BackRequested += () =>
				{
					// When user clicks Back, we remove the detail control
					SelectedDetail = null;
				};

				SelectedDetail = detailedControl;
			}
		}
		private bool FilterGroundWorkItem(object obj)
		{
			if (obj is GroundworkItem item)
			{
				if (string.IsNullOrEmpty(SearchTerm))
					return true;
				return item.Description?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		}
        private void OnLibraryChanged()
        {
            // RateLibraryStore / ComputeCatalogStore can raise Changed from a background thread.
            var disp = System.Windows.Application.Current?.Dispatcher;
            if (disp == null)
            {
                RecomputeAll();
                return;
            }

            if (disp.CheckAccess())
                RecomputeAll();
            else
                disp.Invoke(RecomputeAll);
        }

        private void RecomputeAll()
        {
            GroundworkItems.Clear();
            BuildGroundWorkItems();
            GroundworkCollectionView?.Refresh();
        }

        /// <summary>
        /// Re-runs the matching ComputeItemN method for the supplied ItemNo and updates the
        /// existing Item instance in place so the open popup keeps its DataContext binding live.
        /// </summary>
        public void RecomputeItemInPlace(int itemNo)
        {
            var existing = GroundworkItems.FirstOrDefault(i => i.ItemNo == itemNo);
            if (existing == null) return;

            Func<GroundworkItem>[] all =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16, ComputeItem17, ComputeItem18,
            };

            GroundworkItem? fresh = null;
            foreach (var fn in all)
            {
                var candidate = fn();
                if (candidate.ItemNo == itemNo) { fresh = candidate; break; }
            }
            if (fresh == null) return;

            existing.NetCost = fresh.NetCost;
            existing.OverheadValue = fresh.OverheadValue;
            existing.ProfitValue = fresh.ProfitValue;
            existing.TotalCost = fresh.TotalCost;

            existing.BreakdownLines.Clear();
            foreach (var line in fresh.BreakdownLines)
                existing.BreakdownLines.Add(line);

            GroundworkCollectionView?.Refresh();
        }


        // ────── FILTER – order by Net Cost (low → high) ──────
        private void ToggleNetCostFilter()
		{
			_isNetCostFilterOn = !_isNetCostFilterOn;

			GroundworkCollectionView.SortDescriptions.Clear();

			if (_isNetCostFilterOn)
				GroundworkCollectionView.SortDescriptions.Add(
					new SortDescription(nameof(GroundworkItem.NetCost),
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

			GroundworkCollectionView.SortDescriptions.Clear();

			switch (_currentSort)
			{
				case SortState.Overhead:
					GroundworkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(GroundworkItem.OverheadValue),
											ListSortDirection.Ascending));
					break;

				case SortState.TotalCost:
					GroundworkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(GroundworkItem.TotalCost),
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

		private void BuildGroundWorkItems()
		{
			//GroundworkItems.Add(ComputeItem1());
			//GroundworkItems.Add(ComputeItem2());
			//GroundworkItems.Add(ComputeItem3());
			//GroundworkItems.Add(ComputeItem4());
			//GroundworkItems.Add(ComputeItem5());
			//GroundworkItems.Add(ComputeItem6());
			//GroundworkItems.Add(ComputeItem7());
			//GroundworkItems.Add(ComputeItem8());
			//GroundworkItems.Add(ComputeItem9());
			//GroundworkItems.Add(ComputeItem10());
			//GroundworkItems.Add(ComputeItem11());
			//GroundworkItems.Add(ComputeItem12());
			//GroundworkItems.Add(ComputeItem13());
			//GroundworkItems.Add(ComputeItem14());
			//GroundworkItems.Add(ComputeItem15());
			//GroundworkItems.Add(ComputeItem16());
			//GroundworkItems.Add(ComputeItem17());
			//GroundworkItems.Add(ComputeItem18());

            Func<GroundworkItem>[] computeMethods =
{
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16, ComputeItem17, ComputeItem18,

            };

            foreach (var compute in computeMethods)
            {
                GroundworkItems.Add(compute());
            }

            // ✅ Append dynamic compute definitions (from API/disk store)
            AppendApiComputeItems();
            AppendAdminRateItems();


        }
        private void AppendApiComputeItems()
        {
            if (_computeEngine == null) return;
            // Only this section: the store holds every section (see ItemsFor).
            var defs = ComputeCatalogStore.ItemsFor(SectionKey);
            if (defs == null || defs.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] No items loaded for section '{SectionKey}'.");
                return;
            }

            int appended = 0;
            int nextNo = GroundworkItems.Count + 1;

            foreach (var def in defs)
            {
                if (def == null || !def.enabled) continue;

                var key = SectionNormalizer.ToSectionKey(def.section);
                if (key != SectionKey) continue;

				try
				{
                    var computed = _computeEngine.Compute(def);

                    var net = (double)computed.NetCost;
                    var ohp = ApplyOHP(net);

                    var breakdown = new ObservableCollection<GroundworkBreakdownLine>();

                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new GroundworkBreakdownLine
                        {
                            ComponentName = $"{l.Kind}: {l.Name}",
                            Quantity = (double)l.Qty,
                            Unit = string.IsNullOrWhiteSpace(l.Unit) ? "" : l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)l.Total
                        });
                    }

                    if (computed.PoPercent > 0)
                    {
                        breakdown.Add(new GroundworkBreakdownLine
                        {
                            ComponentName = $"Compute PO/Uplift ({computed.PoPercent}%)",
                            Quantity = (double)computed.PoPercent,
                            Unit = "%",
                            UnitPrice = 0,
                            TotalPrice = (double)computed.PoAmount
                        });
                    }

                    if (computed.Warnings.Count > 0)
                    {
                        breakdown.Add(new GroundworkBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new GroundworkBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    GroundworkItems.Add(new GroundworkItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 0),
                        BreakdownLines = breakdown
                    });

                    appended++;
                }
				catch (Exception ex)
				{
                    System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] Skip '{def?.name}': {ex.Message}");
                    continue;
                }


            }

            System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] Appended {appended} item(s) for section '{SectionKey}'.");
        }
        private void AppendAdminRateItems()
        {
            var rates = RateLibraryStore.Items;
            if (rates == null || rates.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] No rate items in store. File={ADLMRateGen.Services.RateLibraryStore.FilePath}");
                return;
            }

            int nextNo = GroundworkItems.Count + 1;
            int appended = 0;

            System.Diagnostics.Debug.WriteLine($"[RateLibrary] Store contains {rates.Count} total rates. Filtering section='{SectionKey}'");

            foreach (var r in rates)
            {
                if (r == null) continue;

                if (!string.Equals(r.SectionKey ?? "", SectionKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                var net = (double)r.NetCost;
                if (!(net > 0)) continue;

                var ohp = ApplyOHP(net);

                var breakdown = new ObservableCollection<GroundworkBreakdownLine>();
                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        breakdown.Add(new GroundworkBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice))
                        });
                    }
                }

                GroundworkItems.Add(new GroundworkItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 0),
                    BreakdownLines = breakdown
                });

                appended++;
            }

            System.Diagnostics.Debug.WriteLine($"[RateLibrary] Appended {appended} admin rate(s) for section '{SectionKey}'.");
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

        #region COMPUTE
        private GroundworkItem ComputeItem1()
        {
            double d8BulldozerQty = UserRateEditStore.Current.Qty(SectionKey, 1, "D8 Bulldozer", 1);
            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Operator", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Oil & Consumables (3% of Diesel)", 1);
            double banksmanQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Banksman", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Labour", 2);

            double d8Cost = GetLabourRate("Bulldozer D8");
            double dieselPrice = GetMaterialPrice("Diesel");
            double operatorCost = GetLabourRate("Heavy plant operator");
            double banksmanCost = GetLabourRate("Heavy vehicle driver");
            double labourCost = GetLabourRate("Semi skilled");
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 1, "Diesel", 304.0);
            double outputPerDay = 1456.0;

            double totalPlantDay = (d8BulldozerQty * d8Cost) + (literPerDay * dieselPrice) +
                (oilPctQty * 0.03 * (literPerDay * dieselPrice)) + (operatorQty * operatorCost) + (banksmanQty * banksmanCost) + (labourQty * labourCost);

            double costPerM2 = totalPlantDay / outputPerDay;
            var ohp = ApplyOHP(costPerM2);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "D8 Bulldozer",
                    Quantity = d8BulldozerQty,
                    Unit = "No/Day",
                    UnitPrice = d8Cost,
                    TotalPrice = d8BulldozerQty * d8Cost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Diesel",
                    Quantity = literPerDay,
                    Unit = "Liters",
                    UnitPrice = dieselPrice,
                    TotalPrice = literPerDay * dieselPrice

                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Operator",
                    Quantity = operatorQty,
                    Unit = "No/Day",
                    UnitPrice = operatorCost,
                    TotalPrice = operatorQty * operatorCost
                },
                new GroundworkBreakdownLine
                {
                  ComponentName = "Oil & Consumables (3% of Diesel)",
                  Quantity = oilPctQty,
                    Unit = "3%",
                    UnitPrice = 0.03 * dieselPrice,
                    TotalPrice = oilPctQty * 0.03 * dieselPrice
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Banksman",
                    Quantity = banksmanQty,
                    Unit = "No/Day",
                    UnitPrice = banksmanCost,
                    TotalPrice = banksmanQty * banksmanCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Labour",
                    Quantity = labourQty,
                    Unit = "No/Day",
                    UnitPrice = labourCost,
                    TotalPrice = labourQty * labourCost
                },
                 new GroundworkBreakdownLine
                {
                    ComponentName = "Total",
                    Quantity = 1,
                    Unit = "Unit",
                    UnitPrice = totalPlantDay,
                    TotalPrice = totalPlantDay
                },
                 new GroundworkBreakdownLine
                {
                    ComponentName = "Total Cost/m2",
                    Quantity = 1,
                    Unit = "Unit",
                    UnitPrice = costPerM2,
                    TotalPrice = costPerM2
                }
            };

            return new GroundworkItem
            {
                ItemNo = 1,
                Description = "Clearing site of bushes and shrubs using D8 or equivalent bulldozer/payloader, " +
                "and shifting material on level ground to a distance not exceeding 100 meters",
                Unit = "m2",
                NetCost = Math.Round(costPerM2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown

            };
        }
        private GroundworkItem ComputeItem2()
        {
            double costPerM3Qty = UserRateEditStore.Current.Qty(SectionKey, 2, "Cost per m3", 1);

            double subCost = ComputeItem1_SubTotal();
            double outputPerDay = UserRateEditStore.Current.Qty(SectionKey, 2, "Output per day", 1820.0);

            double costPerM3 = subCost / outputPerDay;
            var ohp = ApplyOHP(costPerM3);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "Subtotal from Item1 approach",
                    Quantity = 1,
                    Unit = "Lump",
                    UnitPrice = subCost,
                    TotalPrice = subCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Output per day",
                    Quantity = outputPerDay,
                    Unit = "m3/day",
                    UnitPrice = 0,
                    TotalPrice = 0
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Cost per m3",
                    Quantity = costPerM3Qty,
                    Unit = "m3",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total",
                    Quantity = 1,
                    Unit = "Unit",
                    UnitPrice = subCost,
                    TotalPrice = subCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total Cost/m3",
                    Quantity = 1,
                    Unit = "Unit",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                }
            };

            return new GroundworkItem
            {
                ItemNo = 2,
                Description = "Excavation as before but distance not exceeding 50 meters.",
                Unit = "m3",
                NetCost = Math.Round(costPerM3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem3()
        {
            double subItemQty = UserRateEditStore.Current.Qty(SectionKey, 3, "SubItem from Item1 approach", 1);
            double costPerM2Qty = UserRateEditStore.Current.Qty(SectionKey, 3, "Cost per m2", 1);

            double subCost = ComputeItem1_SubTotal();
            double outputPerDay = UserRateEditStore.Current.Qty(SectionKey, 3, "Output per day", 980.0);

            double costPerM2 = subCost / outputPerDay;
            var ohp = ApplyOHP(costPerM2);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "SubItem from Item1 approach",
                    Quantity = subItemQty,
                    Unit = "Lump",
                    UnitPrice = subCost,
                    TotalPrice = subCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Output per day",
                    Quantity = outputPerDay,
                    Unit = "m2/day",
                    UnitPrice = 0,
                    TotalPrice = 0
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Cost per m2",
                    Quantity = costPerM2Qty,
                    Unit = "m2",
                    UnitPrice = costPerM2,
                    TotalPrice = costPerM2
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total",
                    Quantity = 1,
                    Unit = "Unit",
                    UnitPrice = subCost,
                    TotalPrice = subCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total Cost/m2",
                    Quantity = 1,
                    Unit = "Unit",
                    UnitPrice = costPerM2,
                    TotalPrice = costPerM2
                }
            };

            return new GroundworkItem
            {
                ItemNo = 3,
                Description = "Excavate oversite to remove topsoil 150mm deep using D8 or equivalent plant to distance not exceeding 50m",
                Unit = "m2",
                NetCost = Math.Round(costPerM2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem4()
        {
            double labourDuration = UserRateEditStore.Current.Qty(SectionKey, 4, "Skilled/Artisan for excavation", 1.6);
            double wheelDuration = UserRateEditStore.Current.Qty(SectionKey, 4, "Skilled/Artisan for wheel deposit", 1.18);
            double labourCost = GetLabourRate("Skilled/Artisan");
            double ratePerHr = labourCost / 8;

            double excavateLabour = labourDuration * ratePerHr;
            double wheelLabour = wheelDuration * ratePerHr;

            double costPerM3 = excavateLabour + wheelLabour;
            double costPerM2 = costPerM3 * 0.15;
            var ohp = ApplyOHP(costPerM2);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "Skilled/Artisan for excavation",
                    Quantity = labourDuration,
                    Unit = "hr/m3",
                    UnitPrice = ratePerHr,
                    TotalPrice = excavateLabour
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Skilled/Artisan for wheel deposit",
                    Quantity = wheelDuration,
                    Unit = "hr/m3",
                    UnitPrice = ratePerHr,
                    TotalPrice = wheelLabour
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total (m3)",
                    Quantity = 1,
                    Unit = "m3",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total Cost/m2",
                    Quantity = 1,
                    Unit = "m2",
                    UnitPrice = costPerM2,
                    TotalPrice = costPerM2
                }
            };

            return new GroundworkItem
            {
                ItemNo = 4,
                Description = "Excavate oversite by hand to reduce levels in sandy soil and wheel material to dump not exceeding 20m from excavation.",
                Unit = "m2",
                NetCost = Math.Round(costPerM2, 0),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 0),
                BreakdownLines = breakdown
            };

        }
        private GroundworkItem ComputeItem5()
        {
            double workDuration = UserRateEditStore.Current.Qty(SectionKey, 5, "Labourer (soft sand excavation)", 1.4);
            double labourCost = GetLabourRate("Labourer");
            double ratePerHr = labourCost / 8;

            double costPerM3 = workDuration * ratePerHr;
            var ohp = ApplyOHP(costPerM3);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "Labourer (soft sand excavation)",
                    Quantity = workDuration,
                    Unit = "hr/m3",
                    UnitPrice = ratePerHr,
                    TotalPrice = costPerM3
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total",
                    Quantity = 1,
                    Unit = "m3",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total Cost/m3",
                    Quantity = 1,
                    Unit = "m3",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                }
            };

            return new GroundworkItem
            {
                ItemNo = 5,
                Description = "Excavate by hand shallow trench in soft sand for foundation not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(costPerM3, 0),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 0),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem6()
        {
            double workDuration = UserRateEditStore.Current.Qty(SectionKey, 6, "Labourer (stiff clay excavation)", 1.53);
            double labourCost = GetLabourRate("Labourer");
            double ratePerHr = labourCost / 8;
            double rateWithBonus = (ratePerHr * 0.4) + ratePerHr;

            double costPerM3 = workDuration * rateWithBonus;
            var ohp = ApplyOHP(costPerM3);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "Labourer (stiff clay excavation)",
                    Quantity = workDuration,
                    Unit = "hr/m3",
                    UnitPrice = rateWithBonus,
                    TotalPrice = costPerM3
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total",
                    Quantity = 1,
                    Unit = "m3",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total Cost/m3",
                    Quantity = 1,
                    Unit = "m3",
                    UnitPrice = costPerM3,
                    TotalPrice = costPerM3
                }
            };

            return new GroundworkItem
            {
                ItemNo = 6,
                Description = "Excavate shallow by hand trench in stiff clay for foundation not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(costPerM3, 0),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 0),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem7()
        {
            double rollerQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Static steel roller (2.7 to 6 tonnes)", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Oil & Consumables (3% Diesel)", 1);
            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Operator (Heavy plant operator)", 1);
            double banksmanQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Banksman (Semi skilled)", 2);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour", 2);

            double rollerCost = GetLabourRate("Static steel wheeled roller - (2.7 to 6 tonnes)");
            double dieselPrice = GetMaterialPrice("Diesel");
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 7, "Diesel", 150);
            double outputPerDay = 276;
            double fuelCost = dieselPrice * literPerDay;

            double totalPlantDay = (rollerQty * rollerCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (operatorQty * operatorCost) + (banksmanQty * banksmanCost) + (labourQty * labourCost);

            double plantCost = totalPlantDay / outputPerDay;

            double fillingInM2 = UserRateEditStore.Current.Qty(SectionKey, 7, "Filling Sand (Beach)", 0.18);
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "Static steel roller (2.7 to 6 tonnes)",
                    Quantity = rollerQty,
                    Unit = "No/Day",
                    UnitPrice = rollerCost,
                    TotalPrice = rollerQty * rollerCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Diesel",
                    Quantity = literPerDay,
                    Unit = "Liters",
                    UnitPrice = dieselPrice,
                    TotalPrice = fuelCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Oil & Consumables (3% Diesel)",
                    Quantity = oilPctQty,
                    Unit = "",
                    UnitPrice = 0.03*fuelCost,
                    TotalPrice =  oilPctQty * 0.03 * fuelCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Operator (Heavy plant operator)",
                    Quantity = operatorQty,
                    Unit = "No/Day",
                    UnitPrice = operatorCost,
                    TotalPrice = operatorQty * operatorCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Banksman (Semi skilled)",
                    Quantity = banksmanQty,
                    Unit = "No/Day",
                    UnitPrice = banksmanCost,
                    TotalPrice = banksmanQty * banksmanCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Labour",
                    Quantity = labourQty,
                    Unit = "No/Day",
                    UnitPrice = labourCost,
                    TotalPrice = labourQty * labourCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total Plant Cost/m2",
                    Quantity = 1,
                    Unit = "m2",
                    UnitPrice = plantCost,
                    TotalPrice = plantCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Filling Sand (Beach)",
                    Quantity = fillingInM2,
                    Unit = "m3 per m2",
                    UnitPrice = fillingCostPerM3,
                    TotalPrice = fillingPerM2
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Total",
                    Quantity = 1,
                    Unit = "m2",
                    UnitPrice = totalCompact,
                    TotalPrice = totalCompact
                },

            };
            return new GroundworkItem
            {
                ItemNo = 7,
                Description = "Level and compact sand to a maximum thickness of 150mm to a maximum compaction of 100% using static wheel roller (2 to 6 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem8()
        {
            double rollerQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Vibratory Roller (8-10 tons)", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Oil & Consumables (3%)", 1);
            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Operator (Heavy plant operator)", 1);
            double banksmanQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Banksman (Semi skilled)", 2);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Labour", 2);

            double rollerCost = GetLabourRate("Vibratory wheeled roller (8 to 10 tons)");

            double dieselPrice = GetMaterialPrice("Diesel");
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 8, "Diesel", 250);
            double fuelCost = dieselPrice * literPerDay;

            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double outputPerDay = 183;
            double outputVolumePerDay = outputPerDay * .3;

            double totalPlantDay = (rollerQty * rollerCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (operatorQty * operatorCost) + (banksmanQty * banksmanCost) + (labourQty * labourCost);

            double plantCost = totalPlantDay / outputPerDay;

            double fillingInM2 = UserRateEditStore.Current.Qty(SectionKey, 8, "Filling Sand (Beach)", 0.36);
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Vibratory Roller (8-10 tons)", Quantity=rollerQty, Unit="No/Day", UnitPrice=rollerCost, TotalPrice=rollerQty * rollerCost },
                new GroundworkBreakdownLine { ComponentName="Diesel", Quantity=literPerDay, Unit="Liters", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new GroundworkBreakdownLine { ComponentName="Oil & Consumables (3%)", Quantity=oilPctQty, Unit="", UnitPrice=0.03*fuelCost, TotalPrice=oilPctQty * 0.03 * fuelCost },
                new GroundworkBreakdownLine { ComponentName="Operator (Heavy plant operator)", Quantity=operatorQty, Unit="No/Day", UnitPrice=operatorCost, TotalPrice=operatorQty * operatorCost },
                new GroundworkBreakdownLine { ComponentName="Banksman (Semi skilled)", Quantity=banksmanQty, Unit="No/Day", UnitPrice=banksmanCost, TotalPrice=banksmanQty * banksmanCost },
                new GroundworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="No/Day", UnitPrice=labourCost, TotalPrice=labourQty * labourCost },
                new GroundworkBreakdownLine { ComponentName="Total Plant Cost/m2", Quantity=1, Unit="m2", UnitPrice=plantCost, TotalPrice=plantCost },
                new GroundworkBreakdownLine { ComponentName="Filling Sand (Beach)", Quantity=fillingInM2, Unit="m3 per m2", UnitPrice=fillingCostPerM3, TotalPrice=fillingPerM2 },
                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact },
				//new GroundworkBreakdownLine { ComponentName="Total Cost/m2", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact }
			};

            return new GroundworkItem
            {
                ItemNo = 8,
                Description = "Level and compact sand to a maximum thickness of 300mm in two layers of 150mm to a maximum compaction of " +
                "100% using smooth wheel roller (8 to 10 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem9()
        {
            double rollerQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Vibratory Roller (8-10 tons)", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Oil & Consumables (3%)", 1);
            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Operator (Heavy plant operator)", 1);
            double banksmanQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Banksman (Semi skilled)", 2);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Labour", 2);

            double rollerCost = GetLabourRate("Vibratory wheeled roller (8 to 10 tons)");

            double dieselPrice = GetMaterialPrice("Diesel");
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 9, "Diesel", 250);
            double fuelCost = dieselPrice * literPerDay;

            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double outputPerDay = 183;
            double outputVolumePerDay = outputPerDay * .3;

            double totalPlantDay = (rollerQty * rollerCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (operatorQty * operatorCost) + (banksmanQty * banksmanCost) + (labourQty * labourCost);

            double plantCost = totalPlantDay / outputPerDay;

            double fillingInM2 = UserRateEditStore.Current.Qty(SectionKey, 9, "Filling Sand (Beach)", 0.54);
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Vibratory Roller (8-10 tons)", Quantity=rollerQty, Unit="No/Day", UnitPrice=rollerCost, TotalPrice=rollerQty * rollerCost },
                new GroundworkBreakdownLine { ComponentName="Diesel", Quantity=literPerDay, Unit="Liters", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new GroundworkBreakdownLine { ComponentName="Oil & Consumables (3%)", Quantity=oilPctQty, Unit="", UnitPrice=0.03 *dieselPrice, TotalPrice=oilPctQty * 0.03 * dieselPrice },
                new GroundworkBreakdownLine { ComponentName="Operator (Heavy plant operator)", Quantity=operatorQty, Unit="No/Day", UnitPrice=operatorCost, TotalPrice=operatorQty * operatorCost },
                new GroundworkBreakdownLine { ComponentName="Banksman (Semi skilled)", Quantity=banksmanQty, Unit="No/Day", UnitPrice=banksmanCost, TotalPrice=banksmanQty * banksmanCost },
                new GroundworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="No/Day", UnitPrice=labourCost, TotalPrice=labourQty * labourCost },
                new GroundworkBreakdownLine { ComponentName="Total Plant Cost/m2", Quantity=1, Unit="m2", UnitPrice=plantCost, TotalPrice=plantCost },
                new GroundworkBreakdownLine { ComponentName="Filling Sand (Beach)", Quantity=fillingInM2, Unit="m3 per m2", UnitPrice=fillingCostPerM3, TotalPrice=fillingPerM2 },
                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact },
				//new GroundworkBreakdownLine { ComponentName="Total Cost/m2", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact }
			};
            return new GroundworkItem
            {
                ItemNo = 9,
                Description = "Level and compact sand to a maximum thickness of 450mm layers of 150mm to a maximum compaction of 100% using smooth wheel roller (8 to 10 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem10()
        {
            double rollerQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Vibratory Roller (8-10 tons)", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Oil & Consumables (3%)", 1);
            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Operator (Heavy plant operator)", 1);
            double banksmanQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Banksman (Semi skilled)", 2);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Labour", 2);

            double rollerCost = GetLabourRate("Vibratory wheeled roller (8 to 10 tons)");

            double dieselPrice = GetMaterialPrice("Diesel");
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 10, "Diesel", 250);
            double fuelCost = dieselPrice * literPerDay;

            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double outputPerDay = 93;
            double outputVolumePerDay = outputPerDay * .6;

            double totalPlantDay = (rollerQty * rollerCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (operatorQty * operatorCost) + (banksmanQty * banksmanCost) + (labourQty * labourCost);

            double plantCost = totalPlantDay / outputVolumePerDay;

            double fillingInM2 = UserRateEditStore.Current.Qty(SectionKey, 10, "Filling Sand (Beach)", 0.72);
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Vibratory Roller (8-10 tons)", Quantity=rollerQty, Unit="No/Day", UnitPrice=rollerCost, TotalPrice=rollerQty * rollerCost },
                new GroundworkBreakdownLine { ComponentName="Diesel", Quantity=literPerDay, Unit="Liters", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new GroundworkBreakdownLine { ComponentName="Oil & Consumables (3%)", Quantity=oilPctQty, Unit="", UnitPrice=0.03 * fuelCost, TotalPrice=oilPctQty * 0.03 * fuelCost },
                new GroundworkBreakdownLine { ComponentName="Operator (Heavy plant operator)", Quantity=operatorQty, Unit="No/Day", UnitPrice=operatorCost, TotalPrice=operatorQty * operatorCost },
                new GroundworkBreakdownLine { ComponentName="Banksman (Semi skilled)", Quantity=banksmanQty, Unit="No/Day", UnitPrice=banksmanCost, TotalPrice=banksmanQty * banksmanCost },
                new GroundworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="No/Day", UnitPrice=labourCost, TotalPrice=labourQty * labourCost },
                new GroundworkBreakdownLine { ComponentName="Total Plant Cost/m3", Quantity=1, Unit="m3", UnitPrice=plantCost, TotalPrice=plantCost },
                new GroundworkBreakdownLine { ComponentName="Filling Sand (Beach)", Quantity=fillingInM2, Unit="m3 per m2", UnitPrice=fillingCostPerM3, TotalPrice=fillingPerM2 },
                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact },
				//new GroundworkBreakdownLine { ComponentName="Total Cost/m2", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact }
			};
            return new GroundworkItem
            {
                ItemNo = 10,
                Description = "Level and compact sand to a maximum thickness of 600mm in layers of 150mm to a maximum compaction of 100% using smooth wheel roller (8 to 10 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem11()
        {
            double tipperQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Hire of one no. tipper (per day)", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Oil & Consumables (3%)", 1);
            double tipperDriverQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Tipper Driver (per day)", 1);
            double motorBoyQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Motor-boy (per day)", 2);
            double tripsPerDayQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Number of trips per day", 9);

            double tipperCost = GetLabourRate("Tipping lorry-10 ton");

            double dieselPrice = GetMaterialPrice("Diesel");
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 11, "Diesel", 200);
            double fuelCost = dieselPrice * literPerDay;

            double tipperDriverCost = GetLabourRate("Heavy plant operator") * 1.4;
            double motorBoyCost = GetLabourRate("Semi skilled") * 1.4;

            double tripsPerDay = tripsPerDayQty;

            double soilVolumePerTrip = 7.6;

            double totalPlantDay = (tipperQty * tipperCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (tipperDriverQty * tipperDriverCost) + (motorBoyQty * motorBoyCost);

            double costPerTrip = totalPlantDay / tripsPerDay;
            double costPerCubicMeter = costPerTrip / soilVolumePerTrip;

            //LOADING LABOUR
            double labourPerHr = GetLabourRate("Labourer") / 8;
            double labourLoadingPerM3 = UserRateEditStore.Current.Qty(SectionKey, 11, "Loading Time Per Cubic", 0.15);

            double timeLoadingPerTruck = UserRateEditStore.Current.Qty(SectionKey, 11, "Loading Time Per 10 ton Truck", soilVolumePerTrip * labourLoadingPerM3);
            double labourToLoadTruck = UserRateEditStore.Current.Qty(SectionKey, 11, "Nr of Labour Per 10 ton Truck", 6);
            double costLabourGang = labourPerHr * labourToLoadTruck;
            double timeForAllLabour = UserRateEditStore.Current.Qty(SectionKey, 11, $"{labourToLoadTruck} Labour Loading Time", timeLoadingPerTruck / labourToLoadTruck);
            double costForAllLabour = timeForAllLabour * costLabourGang;

            double soilPerTruck = UserRateEditStore.Current.Qty(SectionKey, 11, "Soil Volume Per Trip", 7.6);

            double labourCostPerm3 = costForAllLabour / soilPerTruck;

            double netCost = costPerCubicMeter + labourCostPerm3;
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Hire of one no. tipper (per day)", Quantity=tipperQty, Unit="No/Day", UnitPrice=tipperCost, TotalPrice=tipperQty * tipperCost },
                new GroundworkBreakdownLine { ComponentName="Diesel", Quantity=literPerDay, Unit="Liters", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new GroundworkBreakdownLine { ComponentName="Oil & Consumables (3%)", Quantity=oilPctQty , Unit="3%", UnitPrice=0.03 * fuelCost, TotalPrice=oilPctQty * 0.03 * fuelCost },
                new GroundworkBreakdownLine { ComponentName="Tipper Driver (per day)", Quantity=tipperDriverQty, Unit="No/Day", UnitPrice=tipperDriverCost, TotalPrice=tipperDriverQty * tipperDriverCost },
                new GroundworkBreakdownLine { ComponentName="Motor-boy (per day)", Quantity=motorBoyQty, Unit="No/Day", UnitPrice=motorBoyCost, TotalPrice=motorBoyQty * motorBoyCost },

                new GroundworkBreakdownLine { ComponentName="Number of trips per day", Quantity=tripsPerDayQty, Unit="trips/day"},
                new GroundworkBreakdownLine { ComponentName="Cost Per Trip", Quantity=UserRateEditStore.Current.Qty(SectionKey, 11, "Cost Per Trip", 1), Unit="N", UnitPrice=costPerTrip},
                new GroundworkBreakdownLine { ComponentName="Soil Volume Per Trip", Quantity=soilPerTruck, Unit="m3"},
                new GroundworkBreakdownLine { ComponentName="Total Transport Cost per M3", Quantity=1, Unit="m3",  TotalPrice=costPerCubicMeter },

                new GroundworkBreakdownLine { ComponentName="Labourer (Loading per Hour)", Quantity=UserRateEditStore.Current.Qty(SectionKey, 11, "Labourer (Loading per Hour)", 1), Unit="N/hr", UnitPrice=labourPerHr },
                new GroundworkBreakdownLine { ComponentName="Loading Time Per Cubic", Quantity=labourLoadingPerM3, Unit="hr", UnitPrice=labourLoadingPerM3 },
                new GroundworkBreakdownLine { ComponentName="Loading Time Per 10 ton Truck", Quantity=timeLoadingPerTruck, Unit="hr", UnitPrice=timeLoadingPerTruck },
                new GroundworkBreakdownLine { ComponentName="Nr of Labour Per 10 ton Truck", Quantity=labourToLoadTruck, Unit="Nr", UnitPrice=labourToLoadTruck },
                new GroundworkBreakdownLine { ComponentName="Labour Cost Per 10 ton Truck", Quantity=UserRateEditStore.Current.Qty(SectionKey, 11, "Labour Cost Per 10 ton Truck", 1), Unit="N/hr", UnitPrice=costLabourGang },
                new GroundworkBreakdownLine { ComponentName=$"{labourToLoadTruck} Labour Loading Time", Quantity=timeForAllLabour, Unit="hr/Trip", UnitPrice=costLabourGang, TotalPrice=costForAllLabour },
                new GroundworkBreakdownLine { ComponentName=$"Soil Volume Per 10 ton Truck", Quantity=soilPerTruck, Unit="m3" },
                new GroundworkBreakdownLine { ComponentName="Total Labour Loading Cost per M3", Quantity=1, Unit="m3",  TotalPrice=labourCostPerm3 },


                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCost },
				//new GroundworkBreakdownLine { ComponentName="Total Cost/m2", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact }
			};
            return new GroundworkItem
            {
                ItemNo = 11,
                Description = "Remove excess excavated material from site, tip location not exceeding 10km form site, using 10ton tipping lorry, manual loading.",
                Unit = "m3",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem12()
        {
            double tipperQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Hire of one no. tipper (per day)", 1);
            double oilPctQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Oil & Consumables (3%)", 1);
            double tipperDriverQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Tipper Driver (per day)", 1);
            double motorBoyQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Motor-boy (per day)", 2);
            double payLoaderQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Hire of one no. payloader (per day)", 1);
            double loaderOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Operator (per day)", 1);
            double loaderBanksmanQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Banksman (per day)", 2);

            double tipperCost = GetLabourRate("Tipping lorry-10 ton");

            double dieselPrice = GetMaterialPrice("Diesel");
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 12, "Diesel", 200);
            double fuelCost = dieselPrice * literPerDay;

            double tipperDriverCost = GetLabourRate("Heavy plant operator") * 1.4;
            double motorBoyCost = GetLabourRate("Semi skilled") * 1.4;

            double tripsPerDay = UserRateEditStore.Current.Qty(SectionKey, 12, "Number of trips per day", 12);

            double soilVolumePerTrip = UserRateEditStore.Current.Qty(SectionKey, 12, "Soil Volume Per Trip", 7.6);

            double totalPlantDay = (tipperQty * tipperCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (tipperDriverQty * tipperDriverCost) + (motorBoyQty * motorBoyCost);

            double costPerTrip = Math.Round(totalPlantDay / tripsPerDay, 2);
            double costPerCubicMeter = Math.Round(costPerTrip / soilVolumePerTrip, 2);

            //LOADING PLANT COST
            double payLoaderCost = GetLabourRate("Payloader 935");
            double totalPlantDayLoading = (payLoaderQty * payLoaderCost) + fuelCost +
                (oilPctQty * 0.03 * fuelCost) + (loaderOperatorQty * tipperDriverCost) + (loaderBanksmanQty * motorBoyCost);

            double payloadWorkHrPerDay = UserRateEditStore.Current.Qty(SectionKey, 12, "Total worked hours per day", 8);

            double payLoadSoilVolumePerDay = UserRateEditStore.Current.Qty(SectionKey, 12, "Bucket capacity of payloader", 7.6);

            double costPerLoadDay = Math.Round(totalPlantDayLoading / payloadWorkHrPerDay, 2);
            double volumePerDay = UserRateEditStore.Current.Qty(SectionKey, 12, "Volume of soil loaded per day", Math.Round(tripsPerDay * payLoadSoilVolumePerDay, 2));
            double payLoadCostPerDay = Math.Round(costPerLoadDay / volumePerDay, 2);


            double netCost = costPerCubicMeter + payLoadCostPerDay;
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Hire of one no. tipper (per day)", Quantity=tipperQty, Unit="No/Day", UnitPrice=tipperCost, TotalPrice=tipperQty * tipperCost },
                new GroundworkBreakdownLine { ComponentName="Diesel", Quantity=literPerDay, Unit="Liters", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new GroundworkBreakdownLine { ComponentName="Oil & Consumables (3%)", Quantity=oilPctQty , Unit="3%", UnitPrice=0.03 * fuelCost, TotalPrice=oilPctQty * 0.03 * fuelCost },
                new GroundworkBreakdownLine { ComponentName="Tipper Driver (per day)", Quantity=tipperDriverQty, Unit="No/Day", UnitPrice=tipperDriverCost, TotalPrice=tipperDriverQty * tipperDriverCost },
                new GroundworkBreakdownLine { ComponentName="Motor-boy (per day)", Quantity=motorBoyQty, Unit="No/Day", UnitPrice=motorBoyCost, TotalPrice=motorBoyQty * motorBoyCost },

                new GroundworkBreakdownLine { ComponentName="Number of trips per day", Quantity=tripsPerDay, Unit="trips/day"},
                new GroundworkBreakdownLine { ComponentName="Cost Per Trip", Quantity=UserRateEditStore.Current.Qty(SectionKey, 12, "Cost Per Trip", 1), Unit="N", UnitPrice=costPerTrip},
                new GroundworkBreakdownLine { ComponentName="Soil Volume Per Trip", Quantity=soilVolumePerTrip, Unit="m3"},
                new GroundworkBreakdownLine { ComponentName="Total Transport Cost per M3", Quantity=1, Unit="m3",  TotalPrice=costPerCubicMeter },

				//PLANT COST
				new GroundworkBreakdownLine { ComponentName="Hire of one no. payloader (per day)", Quantity=payLoaderQty, Unit="No/Day", UnitPrice=payLoaderCost, TotalPrice=payLoaderQty * payLoaderCost },
                new GroundworkBreakdownLine { ComponentName="Diesel", Quantity=literPerDay, Unit="Liters", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new GroundworkBreakdownLine { ComponentName="Oil & Consumables (3%)", Quantity=oilPctQty , Unit="3%", UnitPrice=0.03 * fuelCost, TotalPrice=oilPctQty * 0.03 * fuelCost },
                new GroundworkBreakdownLine { ComponentName="Operator (per day)", Quantity=loaderOperatorQty, Unit="No/Day", UnitPrice=tipperDriverCost, TotalPrice=loaderOperatorQty * tipperDriverCost },
                new GroundworkBreakdownLine { ComponentName="Banksman (per day)", Quantity=loaderBanksmanQty, Unit="No/Day", UnitPrice=motorBoyCost, TotalPrice=loaderBanksmanQty * motorBoyCost },

                new GroundworkBreakdownLine { ComponentName="Total worked hours per day", Quantity=payloadWorkHrPerDay, Unit="hrs"},
                new GroundworkBreakdownLine { ComponentName="Bucket capacity of payloader", Quantity=payLoadSoilVolumePerDay, Unit="m3", UnitPrice=costPerLoadDay},
                new GroundworkBreakdownLine { ComponentName="Volume of soil loaded per day", Quantity=volumePerDay, Unit="m3"},
                new GroundworkBreakdownLine { ComponentName="Total cost per m3 of material hauled .", Quantity=1, Unit="m3",  TotalPrice=payLoadCostPerDay },


                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCost },
				//new GroundworkBreakdownLine { ComponentName="Total Cost/m2", Quantity=1, Unit="m2", UnitPrice=totalCompact, TotalPrice=totalCompact }
			};
            return new GroundworkItem
            {
                ItemNo = 12,
                Description = "Remove excess excavated material from site, tip location not exceeding 10km form site, using 10ton tipping lorry, loading done with payloader caterpillar 935 or equivalent model specification.",
                Unit = "m3",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem13()
        {
            double labourPerHr = UserRateEditStore.Current.Qty(SectionKey, 13, "Labour (per hour)", 0.98);
            double labourCost = (GetLabourRate("Labourer") / 8) + 90;

            double netCost = labourPerHr * labourCost;
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Labour (per hour)", Quantity=labourPerHr, Unit="hr/m3", UnitPrice=labourCost, TotalPrice=netCost },

                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCost },
            };
            return new GroundworkItem
            {
                ItemNo = 13,
                Description = "Backfill previously excavated material in soft sand for foundation trench not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem14()
        {
            double labourPerHr = UserRateEditStore.Current.Qty(SectionKey, 14, "Labour (per hour)", 1.07);
            double labourCost = (GetLabourRate("Labourer") / 8) + 90;

            double netCost = Math.Round(labourPerHr * labourCost, 2);
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Labour (per hour)", Quantity=labourPerHr, Unit="hr/m3", UnitPrice=labourCost, TotalPrice=netCost },

                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCost },
            };
            return new GroundworkItem
            {
                ItemNo = 14,
                Description = "Backfill previously excavated material in stiff clay for foundation trench not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem15()
        {
            double sharpSandQty = UserRateEditStore.Current.Qty(SectionKey, 15, "Sharp sand (delivered to site)", 1);

            double materialCost = GetMaterialPrice("Sharp Sand");

            double labourPerHr = UserRateEditStore.Current.Qty(SectionKey, 15, "Labour (per hour)", 0.77);
            double labourCost = (GetLabourRate("Labourer") / 8) + 90;
            double labourTotal = (labourPerHr * labourCost);

            double netCost = Math.Round(labourTotal + (sharpSandQty * materialCost), 2);
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Sharp sand (delivered to site)", Quantity=sharpSandQty, Unit="m3", UnitPrice=materialCost, TotalPrice=sharpSandQty * materialCost },
                new GroundworkBreakdownLine { ComponentName="Labour (per hour)", Quantity=labourPerHr, Unit="hr/m3", UnitPrice=labourCost, TotalPrice=labourTotal },

                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCost },
            };
            return new GroundworkItem
            {
                ItemNo = 15,
                Description = "Backfill imported filling sand (sharp sand) in foundation trench not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem16()
        {
            double beachSandQty = UserRateEditStore.Current.Qty(SectionKey, 16, "Beach sand (delivered to site)", 1);

            double materialCost = GetMaterialPrice("Beach sand");

            double labourPerHr = UserRateEditStore.Current.Qty(SectionKey, 16, "Labour (per hour)", 0.77);
            double labourCost = (GetLabourRate("Labourer") / 8) + 90;
            double labourTotal = (labourPerHr * labourCost);

            double netCost = Math.Round(labourTotal + (beachSandQty * materialCost), 2);
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Beach sand (delivered to site)", Quantity=beachSandQty, Unit="m3", UnitPrice=materialCost, TotalPrice=beachSandQty * materialCost },
                new GroundworkBreakdownLine { ComponentName="Labour (per hour)", Quantity=labourPerHr, Unit="hr/m3", UnitPrice=labourCost, TotalPrice=labourTotal },

                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCost },
            };
            return new GroundworkItem
            {
                ItemNo = 16,
                Description = "Imported filling sand (beach sand) for making up levels.",
                Unit = "m3",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem17()
        {
            double labourPerHr = UserRateEditStore.Current.Qty(SectionKey, 17, "Labour (per hour)", 0.25);
            double labourCost = (GetLabourRate("Labourer") / 8) + 90;

            double netCost = Math.Round(labourPerHr * labourCost, 2);
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Labour (per hour)", Quantity=labourPerHr, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=netCost },

                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCost },
            };
            return new GroundworkItem
            {
                ItemNo = 17,
                Description = "Level and compact bottom of trench in readiness for concrete: Excavate trench.",
                Unit = "m2",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private GroundworkItem ComputeItem18()
        {
            double hardcorePerM3 = UserRateEditStore.Current.Qty(SectionKey, 18, "Hardcore or  (cubic metre content in 1m2 of filling, 150mm thick)", 0.192);
            double materialCost = GetMaterialPrice("15-25mm");
            double materialCost2 = GetMaterialPrice("Hardcore filling");
            double materialSum = (materialCost + materialCost2) * 1.474;

            double totalMaterialCost = hardcorePerM3 * materialSum;

            double labourPerHr = UserRateEditStore.Current.Qty(SectionKey, 18, "Labour (per hour)", 0.88);
            double labourCost = (GetLabourRate("Labourer") / 8) + 90;
            double labourTotal = (labourPerHr * labourCost);

            double netCost = Math.Round(labourTotal + totalMaterialCost, 2);
            var ohp = ApplyOHP(netCost);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine { ComponentName="Hardcore or  (cubic metre content in 1m2 of filling, 150mm thick)", Quantity=hardcorePerM3, Unit="m2" },
                new GroundworkBreakdownLine { ComponentName="Cost per m3 (including transportation)", Quantity=UserRateEditStore.Current.Qty(SectionKey, 18, "Cost per m3 (including transportation)", 1), Unit="m3", UnitPrice=materialSum  },
                new GroundworkBreakdownLine { ComponentName="Total Material Cost", Quantity=1, Unit="m3", TotalPrice=totalMaterialCost  },

                new GroundworkBreakdownLine { ComponentName="Labour (per hour)", Quantity=labourPerHr, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourTotal },

                new GroundworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCost },
            };
            return new GroundworkItem
            {
                ItemNo = 18,
                Description = "Level and manually compact  hardcore to a maximum thickness of 150mm.",
                Unit = "m2",
                NetCost = Math.Round(netCost, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BreakdownLines = breakdown
            };
        }
        private double ComputeItem1_SubTotal()
        {
            double d8Cost = GetLabourRate("Bulldozer D8");
            double dieselPrice = GetMaterialPrice("Diesel");
            double operatorCost = GetLabourRate("Heavy plant operator");
            double banksmanCost = GetLabourRate("Heavy vehicle driver");
            double labourCost = GetLabourRate("Semi skilled");
            double literPerDay = 304.0;

            double subTotal = d8Cost + (literPerDay * dieselPrice) +
                (0.03 * (literPerDay * dieselPrice)) + operatorCost + banksmanCost + (2 * labourCost);

            return subTotal;
        }

        #endregion
    }
}
