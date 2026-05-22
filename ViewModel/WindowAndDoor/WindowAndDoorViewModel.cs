// WindowAndDoorViewModel.cs
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.WindowAndDoor
{
    public class WindowAndDoorViewModel : ViewModelBase
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

        // ✅ Windows and Doors section key
        private const string SectionKey = SectionKeys.DoorsWindows;

        private readonly ComputeItemEngine _computeEngine;

        public double OverheadPercent
        {
            get => _overheadPercent;
            set
            {
                if (Math.Abs(_overheadPercent - value) > 0.0001)
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
                if (Math.Abs(_profitPercent - value) > 0.0001)
                {
                    _profitPercent = value;
                    RaisePropertyChanged();
                    RecomputeAll();
                }
            }
        }

        public ObservableCollection<WindowAndDoorItem> WindowAndDoorItems { get; set; }
            = new ObservableCollection<WindowAndDoorItem>();

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
                    WindowAndDoorCollectionView?.Refresh();
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
        public ICommand FilterCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand AddCustomRateCommand { get; }

        public WindowAndDoorViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);

            // local libraries
            matLib.LibraryChanged += OnLibraryChanged;
            labourLib.LibraryChanged += OnLibraryChanged;

            // compute engine + compute catalog (API)
            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);
            _ = LoadComputeCatalogForSectionAsync();

            // ✅ admin rate library (API + disk) — same pattern as GroundWorkViewModel
            RateLibraryStore.Changed += OnLibraryChanged;
            _ = LoadRateLibraryAsync();

            BuildWindowAndDoorItem();

            WindowAndDoorCollectionView = CollectionViewSource.GetDefaultView(WindowAndDoorItems);
            WindowAndDoorCollectionView.Filter = FilterWindowAndDoorItem;

            RecomputeCommand = new DelegateCommand(_ => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
            FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
            SortCommand = new DelegateCommand(_ => CycleSort());
            AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
                    RecomputeAll();
            };

            UserRateEditStore.Current.OverridesChanged += (_, __) =>
            {
                void Refresh()
                {
                    var nos = WindowAndDoorItems.Select(i => i.ItemNo).ToList();
                    foreach (var n in nos) RecomputeItemInPlace(n);
                }
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp == null || disp.CheckAccess()) Refresh();
                else disp.BeginInvoke((Action)Refresh);
            };
        }

        // ✅ Same as GroundWorkViewModel: show cached first, then refresh from API
        private async Task LoadRateLibraryAsync()
        {
            try
            {
                // 1) cached disk (offline-friendly)
                RateLibraryStore.ReloadFromDisk();
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] Cached disk items={RateLibraryStore.Items?.Count ?? 0}");

                // 2) API (section filtered)
                var ok = await RateLibraryStore.RefreshFromApiAsync(SectionKey);

                System.Diagnostics.Debug.WriteLine(
                    $"[RateLibrary] Refresh ok={ok}, status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");

                // 3) fallback if section mismatch / inconsistent backend keys
                if (ok && RateLibraryStore.LastApiItemCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[RateLibrary] Section returned 0. Retrying without sectionKey...");
                    await RateLibraryStore.RefreshFromApiAsync();

                    System.Diagnostics.Debug.WriteLine(
                        $"[RateLibrary] Retry(all) status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");
                }

                // refresh UI
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
                var ok = await ComputeCatalogStore.RefreshFromApiAsync(SectionKey);

                if (ok && ComputeCatalogStore.LastApiItemCount == 0)
                {
                    await ComputeCatalogStore.RefreshFromApiAsync(); // fetch all
                }

                RecomputeAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Compute API load failed: {ex}");
            }
        }

        // ✅ Dispatcher-safe (RateLibraryStore/ComputeCatalogStore can fire Changed from background thread)
        private void OnLibraryChanged()
        {
            var disp = Application.Current?.Dispatcher;
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

        private bool FilterWindowAndDoorItem(object obj)
        {
            if (obj is WindowAndDoorItem item)
            {
                if (string.IsNullOrWhiteSpace(SearchTerm))
                    return true;

                return (item.Description ?? "")
                    .IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        private void RecomputeAll()
        {
            WindowAndDoorItems.Clear();
            BuildWindowAndDoorItem();
            WindowAndDoorCollectionView?.Refresh();
        }

        public void RecomputeItemInPlace(int itemNo)
        {
            var existing = WindowAndDoorItems.FirstOrDefault(i => i.ItemNo == itemNo);
            if (existing == null) return;

            Func<WindowAndDoorItem>[] all =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12
            };

            WindowAndDoorItem? fresh = null;
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

            existing.WindowAndDoorBreakdownLines.Clear();
            foreach (var line in fresh.WindowAndDoorBreakdownLines)
                existing.WindowAndDoorBreakdownLines.Add(line);

            WindowAndDoorCollectionView?.Refresh();
        }

        private void ShowDetails(object o)
        {
            if (o is WindowAndDoorItem item)
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

        // ────── FILTER: order by NetCost ──────
        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            WindowAndDoorCollectionView.SortDescriptions.Clear();

            if (_isNetCostFilterOn)
                WindowAndDoorCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(WindowAndDoorItem.NetCost), ListSortDirection.Ascending));
        }

        // ────── SORT: cycle None → Overhead → TotalCost ──────
        private void CycleSort()
        {
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
                        new SortDescription(nameof(WindowAndDoorItem.OverheadValue), ListSortDirection.Ascending));
                    break;

                case SortState.TotalCost:
                    WindowAndDoorCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(WindowAndDoorItem.TotalCost), ListSortDirection.Ascending));
                    break;

                case SortState.None:
                default:
                    break;
            }
        }

        private void OpenCustomRateEntry()
        {
            var view = new CustomRateEntryView();
            view.DataContext = new CustomRateEntryViewModel();
            SelectedDetail = view;
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

        private void BuildWindowAndDoorItem()
        {
            Func<WindowAndDoorItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12
            };

            foreach (var compute in computeMethods)
                WindowAndDoorItems.Add(compute());

            // ✅ Append dynamic compute definitions (API/disk compute catalog)
            AppendApiComputeItems();

            // ✅ Append admin rates (API/disk rate library) — NEW
            AppendAdminRateItems();
        }

        private void AppendApiComputeItems()
        {
            if (_computeEngine == null) return;

            var defs = ComputeCatalogStore.Items;
            if (defs == null || defs.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] No items loaded for section '{SectionKey}'.");
                return;
            }

            int appended = 0;
            int nextNo = WindowAndDoorItems.Count + 1;

            foreach (var def in defs)
            {
                if (def == null || !def.enabled) continue;

                var key = SectionNormalizer.ToSectionKey(def.section);
                if (key != SectionKey) continue;

                try
                {
                    var computed = _computeEngine.Compute(def);

                    var net = (double)computed.NetCost;
                    if (!(net > 0)) continue;

                    var ohp = ApplyOHP(net);

                    var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>();
                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new WindowAndDoorBreakdownLine
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
                        breakdown.Add(new WindowAndDoorBreakdownLine
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
                        breakdown.Add(new WindowAndDoorBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new WindowAndDoorBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    WindowAndDoorItems.Add(new WindowAndDoorItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 0),
                        WindowAndDoorBreakdownLines = breakdown
                    });

                    appended++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] Skip '{def?.name}': {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] Appended {appended} item(s) for section '{SectionKey}'.");
        }

        // ✅ NEW: same logic as GroundWorkViewModel.AppendAdminRateItems
        private void AppendAdminRateItems()
        {
            var rates = RateLibraryStore.Items;
            if (rates == null || rates.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] No rate items in store. File={RateLibraryStore.FilePath}");
                return;
            }

            int nextNo = WindowAndDoorItems.Count + 1;
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

                var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>();
                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        breakdown.Add(new WindowAndDoorBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice))
                        });
                    }
                }

                WindowAndDoorItems.Add(new WindowAndDoorItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 0),
                    WindowAndDoorBreakdownLines = breakdown
                });

                appended++;
            }

            System.Diagnostics.Debug.WriteLine($"[RateLibrary] Appended {appended} admin rate(s) for section '{SectionKey}'.");
        }

        #region Compute Methods (your existing items)

        private WindowAndDoorItem ComputeItem1()
        {
            double windowCost = GetMaterialPrice("Window size 1800 x 1200mm high");
            double windowQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Window size 1800 x 1200mm high.", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 1, "Add for Transportation to site and handling", 5);

            double windowRate = windowCost * windowQty;
            double windowTransport = windowRate * (transportPer / 100);
            double totalMaterialCost = windowRate + windowTransport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double outputPerWindow = UserRateEditStore.Current.Qty(SectionKey, 1, "Time per window.", 1.5);
            double labourPerwindow = glazierLabourRate * outputPerWindow;

            double netCostPerWindow = totalMaterialCost + labourPerwindow;
            var ohp = ApplyOHP(netCostPerWindow);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Window size 1800 x 1200mm high.", Quantity=windowQty, Unit="no", UnitPrice= windowCost, TotalPrice=windowRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=windowTransport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labourPerwindow},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
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
            double windowCost = GetMaterialPrice("Window size 1200 x 1200mm high");
            double windowQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Window size 1200 x 1200mm high.", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 2, "Add for Transportation to site and handling", 5);

            double windowRate = windowCost * windowQty;
            double windowTransport = windowRate * (transportPer / 100);
            double totalMaterialCost = windowRate + windowTransport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double outputPerWindow = UserRateEditStore.Current.Qty(SectionKey, 2, "Time per window.", 1.5);
            double labourPerwindow = glazierLabourRate * outputPerWindow;

            double netCostPerWindow = totalMaterialCost + labourPerwindow;
            var ohp = ApplyOHP(netCostPerWindow);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Window size 1200 x 1200mm high.", Quantity=windowQty, Unit="no", UnitPrice= windowCost, TotalPrice=windowRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=windowTransport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labourPerwindow},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
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
            double windowCost = GetMaterialPrice("Window size 2400 x 1200mm high");
            double windowQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Window size 2400 x 1200mm high.", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 3, "Add for Transportation to site and handling", 5);

            double windowRate = windowCost * windowQty;
            double windowTransport = windowRate * (transportPer / 100);
            double totalMaterialCost = windowRate + windowTransport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double outputPerWindow = UserRateEditStore.Current.Qty(SectionKey, 3, "Time per window.", 2.25);
            double labourPerwindow = glazierLabourRate * outputPerWindow;

            double netCostPerWindow = totalMaterialCost + labourPerwindow;
            var ohp = ApplyOHP(netCostPerWindow);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Window size 2400 x 1200mm high.", Quantity=windowQty, Unit="no", UnitPrice= windowCost, TotalPrice=windowRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=windowTransport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labourPerwindow},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
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
            double windowCost = GetMaterialPrice("Window size 900 x 600mm high");
            double windowQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Window size 900 x 600mm high.", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 4, "Add for Transportation to site and handling", 5);

            double windowRate = windowCost * windowQty;
            double windowTransport = windowRate * (transportPer / 100);
            double totalMaterialCost = windowRate + windowTransport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double outputPerWindow = UserRateEditStore.Current.Qty(SectionKey, 4, "Time per window.", 0.75);
            double labourPerwindow = glazierLabourRate * outputPerWindow;

            double netCostPerWindow = totalMaterialCost + labourPerwindow;
            var ohp = ApplyOHP(netCostPerWindow);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Window size 900 x 600mm high.", Quantity=windowQty, Unit="no", UnitPrice= windowCost, TotalPrice=windowRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=windowTransport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per window.", Quantity=outputPerWindow, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labourPerwindow},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per window", Unit="No", TotalPrice=netCostPerWindow}
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
            double doorCost = GetMaterialPrice("Single swing door size 900 x 2100mm high (Clear Sheet Glazing)");
            double qty = UserRateEditStore.Current.Qty(SectionKey, 5, "Single swing door size 900 x 2100mm high (Clear Sheet Glazing)", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add for Transportation to site and handling", 5);

            double rate = doorCost * qty;
            double transport = rate * (transportPer / 100);
            double totalMaterialCost = rate + transport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double time = UserRateEditStore.Current.Qty(SectionKey, 5, "Time per door.", 1.5);
            double labour = glazierLabourRate * time;

            double net = totalMaterialCost + labour;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Single swing door size 900 x 2100mm high (Clear Sheet Glazing)", Quantity=qty, Unit="no", UnitPrice=doorCost, TotalPrice=rate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per door.", Quantity=time, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labour},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per door", Unit="No", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 5,
                Description = "Supply and install natural anodised clear sheet glazed, sinlge swing entrance door size 900 x 2100mm - GMP",
                Unit = "No",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        private WindowAndDoorItem ComputeItem6()
        {
            double doorCost = GetMaterialPrice("Double leaf single swing door size 1500 x 2100mm high (Clear Sheet Glazing)");
            double qty = UserRateEditStore.Current.Qty(SectionKey, 6, "Double leaf single swing door size 1500 x 2100mm high (Clear Sheet Glazing)", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 6, "Add for Transportation to site and handling", 5);

            double rate = doorCost * qty;
            double transport = rate * (transportPer / 100);
            double totalMaterialCost = rate + transport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double time = UserRateEditStore.Current.Qty(SectionKey, 6, "Time per door.", 1.65);
            double labour = glazierLabourRate * time;

            double net = totalMaterialCost + labour;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Double leaf single swing door size 1500 x 2100mm high (Clear Sheet Glazing)", Quantity=qty, Unit="no", UnitPrice=doorCost, TotalPrice=rate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per door.", Quantity=time, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labour},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per door", Unit="No", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 6,
                Description = "Supply and install natural anodised clear sheet glazed, double swing entrance door size 1500 x 2100mm - GMP",
                Unit = "No",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        private WindowAndDoorItem ComputeItem7()
        {
            double doorCost = GetMaterialPrice("Double leaf single swing door size 1800 x 2100mm high (Clear Sheet Glazing)");
            double qty = UserRateEditStore.Current.Qty(SectionKey, 7, "Double leaf single swing door size 1800 x 2100mm high (Clear Sheet Glazing)", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 7, "Add for Transportation to site and handling", 5);

            double rate = doorCost * qty;
            double transport = rate * (transportPer / 100);
            double totalMaterialCost = rate + transport;

            double glazierLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double glazierLabourQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Tradesman (Glaziers)", 2);
            double glazierLabourRate = glazierLabourCost * glazierLabourQty;

            double time = UserRateEditStore.Current.Qty(SectionKey, 7, "Time per door.", 1.75);
            double labour = glazierLabourRate * time;

            double net = totalMaterialCost + labour;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Double leaf single swing door size 1800 x 2100mm high (Clear Sheet Glazing)", Quantity=qty, Unit="no", UnitPrice=doorCost, TotalPrice=rate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Glaziers)", Quantity=glazierLabourQty, Unit="N/hr", UnitPrice=glazierLabourCost, TotalPrice=glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= glazierLabourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per door.", Quantity=time, Unit="hr/no.", UnitPrice=glazierLabourRate, TotalPrice= labour},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per door", Unit="No", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 7,
                Description = "Supply and install natural anodised clear sheet glazed, double swing entrance door size 1800 x 2100mm - GMP",
                Unit = "No",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        private WindowAndDoorItem ComputeItem8()
        {
            double doorCost = GetMaterialPrice("44mm Solid core flush door (900 x 2100mm)");
            double qty = UserRateEditStore.Current.Qty(SectionKey, 8, "44mm Double faced paneled door (900 x 2100mm)", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 8, "Add for Transportation to site and handling", 5);

            double rate = doorCost * qty;
            double transport = rate * (transportPer / 100);
            double totalMaterialCost = rate + transport;

            double carpenterCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Labour", 1);

            double carpenterRate = carpenterCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double totalLabourCost = carpenterRate + labourRate;

            double time = UserRateEditStore.Current.Qty(SectionKey, 8, "Time per door.", 0.83);
            double labour = totalLabourCost * time;

            double net = totalMaterialCost + labour;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="44mm Double faced paneled door (900 x 2100mm)", Quantity=qty, Unit="no", UnitPrice= doorCost, TotalPrice=rate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr", UnitPrice=carpenterCost, TotalPrice=carpenterRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost, TotalPrice=labourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per door.", Quantity=time, Unit="hr/no.", UnitPrice=totalLabourCost, TotalPrice= labour},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per door", Unit="No", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 8,
                Description = "Supply and install 44mm Timber flush door size 900 x 2100mm high.",
                Unit = "No",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        private WindowAndDoorItem ComputeItem9()
        {
            double doorCost = GetMaterialPrice("Door (750 x 2100mm)");
            double qty = UserRateEditStore.Current.Qty(SectionKey, 9, "Ditto size (750 x 2100mm)", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 9, "Add for Transportation to site and handling", 5);

            double rate = doorCost * qty;
            double transport = rate * (transportPer / 100);
            double totalMaterialCost = rate + transport;

            double carpenterCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Labour", 1);

            double carpenterRate = carpenterCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double totalLabourCost = carpenterRate + labourRate;

            double time = UserRateEditStore.Current.Qty(SectionKey, 9, "Time per door.", 0.83);
            double labour = totalLabourCost * time;

            double net = totalMaterialCost + labour;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Ditto size (750 x 2100mm)", Quantity=qty, Unit="no", UnitPrice= doorCost, TotalPrice=rate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr", UnitPrice=carpenterCost, TotalPrice=carpenterRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost, TotalPrice=labourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per door.", Quantity=time, Unit="hr/no.", UnitPrice=totalLabourCost, TotalPrice= labour},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per door", Unit="No", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 9,
                Description = "Supply and install 44mm Timber flush door size 750 x 2100mm high.",
                Unit = "No",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        private WindowAndDoorItem ComputeItem10()
        {
            double frameCost = GetMaterialPrice("2x4\"x12' (50x100x4200mm)");
            double solignumCost = GetMaterialPrice("Solignum (normal)") / 12;

            double frameQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Frame size 50 x 225 x 3600mm", 1);
            double solignumQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Allow for treating with solignum", 0.05 * 0.225 * 3.6);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 10, "Add for Transportation to site and handling", 5);

            double frameRate = frameCost * frameQty;
            double smoothingCost = solignumCost * .3;
            double frameTransport = frameRate * (transportPer / 100);
            double solignumRate = solignumCost * solignumQty;

            double woodLength = UserRateEditStore.Current.Qty(SectionKey, 10, "Cost per m of frame", 3.6);

            double totalMaterialCost = frameRate + smoothingCost + frameTransport + solignumRate;
            double lengthPerM = totalMaterialCost / woodLength;

            double carpenterCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Labour", 1);

            double carpenterRate = carpenterCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double totalLabourCost = carpenterRate + labourRate;

            double time = UserRateEditStore.Current.Qty(SectionKey, 10, "Time (converted to per m)", 0.76);
            double labourPerM = (totalLabourCost * time) / woodLength;

            double net = lengthPerM + labourPerM;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Frame size 50 x 225 x 3600mm", Quantity=frameQty, Unit="length", UnitPrice= frameCost, TotalPrice=frameRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Allow for planing smooth", TotalPrice=smoothingCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=frameTransport},
                new WindowAndDoorBreakdownLine{ ComponentName="Allow for treating with solignum", Quantity=solignumQty, Unit="m2", UnitPrice= solignumCost, TotalPrice=solignumRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Cost per m of frame", Quantity=woodLength, Unit="m", UnitPrice= totalMaterialCost, TotalPrice=lengthPerM},

                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr", UnitPrice=carpenterCost, TotalPrice=carpenterRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost, TotalPrice=labourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Time (converted to per m)", Quantity=time, Unit="hr/length", UnitPrice=totalLabourCost, TotalPrice= labourPerM},

                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per m", Unit="m", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 10,
                Description = "Supply and install Door Frame size 50 x 225mm complete.",
                Unit = "m",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        private WindowAndDoorItem ComputeItem11()
        {
            double frameCost = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
            double solignumCost = GetMaterialPrice("Solignum (normal)") / 12;

            double frameQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Architrave size 50 x 75 x 3600mm", 1);
            double solignumQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Allow for treating with solignum", 0.05 * 0.075 * 3.6);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 11, "Add for Transportation to site and handling", 5);

            double frameRate = frameCost * frameQty;
            double smoothingCost = solignumCost * .3;
            double frameTransport = frameRate * (transportPer / 100);
            double solignumRate = solignumCost * solignumQty;

            double woodLength = UserRateEditStore.Current.Qty(SectionKey, 11, "Cost per m of architrave", 3.6);

            double totalMaterialCost = frameRate + smoothingCost + frameTransport + solignumRate;
            double lengthPerM = totalMaterialCost / woodLength;

            double carpenterCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Labour", 1);

            double carpenterRate = carpenterCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double totalLabourCost = carpenterRate + labourRate;

            double time = UserRateEditStore.Current.Qty(SectionKey, 11, "Time (converted to per m)", 0.6);
            double labourPerM = (totalLabourCost * time) / woodLength;

            double net = lengthPerM + labourPerM;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Architrave size 50 x 75 x 3600mm", Quantity=frameQty, Unit="length", UnitPrice= frameCost, TotalPrice=frameRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Allow for planing smooth", TotalPrice=smoothingCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=frameTransport},
                new WindowAndDoorBreakdownLine{ ComponentName="Allow for treating with solignum", Quantity=solignumQty, Unit="m2", UnitPrice= solignumCost, TotalPrice=solignumRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Cost per m of architrave", Quantity=woodLength, Unit="m", UnitPrice= totalMaterialCost, TotalPrice=lengthPerM},

                new WindowAndDoorBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr", UnitPrice=carpenterCost, TotalPrice=carpenterRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost, TotalPrice=labourRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice= totalLabourCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Time (converted to per m)", Quantity=time, Unit="hr/length", UnitPrice=totalLabourCost, TotalPrice= labourPerM},

                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per m", Unit="m", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 11,
                Description = "Supply and install Architrave size 50 x 75mm complete.",
                Unit = "m",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        // NOTE: If you already have your own ComputeItem12, keep it and remove this one.
        private WindowAndDoorItem ComputeItem12()
        {
            // Simple placeholder for “Ironmongery set per door” (edit names to match your material library)
            double ironmongeryCost = GetMaterialPrice("Door ironmongery set");
            double qty = UserRateEditStore.Current.Qty(SectionKey, 12, "Door ironmongery set", 1);
            double transportPer = UserRateEditStore.Current.Qty(SectionKey, 12, "Add for Transportation to site and handling", 5);

            double rate = ironmongeryCost * qty;
            double transport = rate * (transportPer / 100);
            double totalMaterialCost = rate + transport;

            double artisanCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double artisanQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Skilled/Artisan", 1);
            double gangRate = artisanCost * artisanQty;

            double time = UserRateEditStore.Current.Qty(SectionKey, 12, "Time per door", 0.5); // hrs/door
            double labour = gangRate * time;

            double net = totalMaterialCost + labour;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<WindowAndDoorBreakdownLine>
            {
                new WindowAndDoorBreakdownLine{ ComponentName="Door ironmongery set", Quantity=qty, Unit="set", UnitPrice=ironmongeryCost, TotalPrice=rate},
                new WindowAndDoorBreakdownLine{ ComponentName="Add for Transportation to site and handling", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new WindowAndDoorBreakdownLine{ ComponentName="Skilled/Artisan", Quantity=artisanQty, Unit="N/hr", UnitPrice=artisanCost, TotalPrice=gangRate},
                new WindowAndDoorBreakdownLine{ ComponentName="Time per door", Quantity=time, Unit="hr/no.", UnitPrice=gangRate, TotalPrice=labour},
                new WindowAndDoorBreakdownLine{ ComponentName="Total Cost per door", Unit="No", TotalPrice=net}
            };

            return new WindowAndDoorItem
            {
                ItemNo = 12,
                Description = "Supply and fix door ironmongery set complete (hinges, lockset, handles) per door.",
                Unit = "No",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                WindowAndDoorBreakdownLines = breakdown
            };
        }

        #endregion
    }
}
