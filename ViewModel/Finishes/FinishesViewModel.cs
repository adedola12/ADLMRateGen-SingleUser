using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Groundwork;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;


namespace ADLMRateGen.ViewModel.Finishes
{
    public class FinishesViewModel : ViewModelBase
    {
        private readonly GetItemsFromDB _helper;
        private readonly BlockworkViewModel _blockworkViewModel;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        private bool _isRecomputing;
        private bool _recomputePending;


        // ─── Sorting / filtering helpers ──────────────────────────────────────────────
        private bool _isNetCostFilterOn = false;          // toggled by “Filter ⌄”
        private SortState _currentSort = SortState.None;  // cycles in “Sort by ⌄”
        private enum SortState { None, Overhead, TotalCost }

        private const string SectionKey = SectionKeys.Finishes;
        // ✅ Finishes section key
        private readonly ComputeItemEngine _computeEngine;

        public ObservableCollection<FinishesItem> FinishesItems { get; set; } =
            new ObservableCollection<FinishesItem>();

        public ICollectionView FinishesCollectionView { get; private set; }

        public double OverheadPercent
        {
            get => _overheadPercent;
            set
            {
                if (Math.Abs(_overheadPercent - value) > 0.000001)
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
                if (Math.Abs(_profitPercent - value) > 0.000001)
                {
                    _profitPercent = value;
                    RaisePropertyChanged();
                    RecomputeAll();
                }
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value ?? string.Empty;
                    RaisePropertyChanged();
                    FinishesCollectionView?.Refresh();
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

        public FinishesViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib, BlockworkViewModel blockworkVM)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);
            _blockworkViewModel = blockworkVM;

            // ✅ Create CollectionView early (so recompute calls are safe)
            FinishesCollectionView = CollectionViewSource.GetDefaultView(FinishesItems);
            FinishesCollectionView.Filter = FilterFinishesItem;

            // Commands
            RecomputeCommand = new DelegateCommand(_ => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
            FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
            SortCommand = new DelegateCommand(_ => CycleSort());
            AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

            // Listen for local material/labour library changes
            matLib.LibraryChanged += OnLibraryChange;
            labourLib.LibraryChanged += OnLibraryChange;

            // ✅ Compute engine
            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);

            // ✅ Compute catalog: disk first, then cloud
            ComputeCatalogStore.ReloadFromDisk();
            ComputeCatalogStore.Changed += OnComputeCatalogChanged;
            _ = LoadComputeCatalogForSectionAsync();

            // ✅ Rate library: disk first, then cloud  ✅ (THIS IS WHAT YOU NEED)
            RateLibraryStore.ReloadFromDisk();
            RateLibraryStore.Changed += OnRateLibraryChanged;
            _ = LoadRateLibraryAsync();

            // Currency changes => rebuild totals
            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
                    RecomputeAll();
            };

            // ✅ Initial build (shows disk cached items immediately)
            RecomputeAll();
        }

        private void OnRateLibraryChanged()
        {
            // RateLibraryStore can raise Changed from background thread
            RunOnUi(RecomputeAll);
        }

        private async Task LoadRateLibraryAsync()
        {
            try
            {
                // Always show cached rates first (offline-friendly)
                RateLibraryStore.ReloadFromDisk();
                Debug.WriteLine($"[RateLibrary] Cached disk items={RateLibraryStore.Items?.Count ?? 0}");

                // Pull latest (requires logged-in token)
                var ok = await RateLibraryStore.RefreshFromApiAsync(SectionKey);

                Debug.WriteLine(
                    $"[RateLibrary] Refresh ok={ok}, status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");

                // Fallback if section mismatch
                if (ok && RateLibraryStore.LastApiItemCount == 0)
                {
                    Debug.WriteLine("[RateLibrary] Section returned 0. Retrying without sectionKey...");
                    await RateLibraryStore.RefreshFromApiAsync();

                    Debug.WriteLine(
                        $"[RateLibrary] Retry(all) status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");
                }

                // Refresh UI
                RunOnUi(RecomputeAll);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RateLibrary] load failed: {ex}");
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


        private void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }
            dispatcher.Invoke(action);
        }

        private void OnComputeCatalogChanged()
        {
            // ensure we update collection on UI thread
            RunOnUi(RecomputeAll);
        }

        #region UI Methods

        private void ShowDetails(object o)
        {
            if (o is FinishesItem item)
            {
                var detailedControl = new FinishesDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () =>
                {
                    SelectedDetail = null;
                };

                SelectedDetail = detailedControl;
            }
        }

        private bool FilterFinishesItem(object obj)
        {
            if (obj is FinishesItem item)
            {
                if (string.IsNullOrWhiteSpace(SearchTerm))
                    return true;

                return (item.Description ?? string.Empty)
                    .IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        private void RecomputeAll()
        {
            RunOnUi(() =>
            {
                // Prevent re-entrant recomputes (ComputeCatalogStore.Changed, currency change, library change, etc.)
                if (_isRecomputing)
                {
                    _recomputePending = true;
                    return;
                }

                _isRecomputing = true;
                try
                {
                    var snapshot = BuildFinishesItemsSnapshot();

                    // IMPORTANT: do NOT DeferRefresh while editing the source collection
                    FinishesItems.Clear();
                    foreach (var it in snapshot)
                        FinishesItems.Add(it);

                    FinishesCollectionView?.Refresh();
                }
                finally
                {
                    _isRecomputing = false;

                    // If something triggered another recompute while we were busy, run once more
                    if (_recomputePending)
                    {
                        _recomputePending = false;
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(RecomputeAll));
                    }
                }
            });
        }

        private List<FinishesItem> BuildFinishesItemsSnapshot()
        {
            var list = new List<FinishesItem>(64);

            Func<FinishesItem>[] computeMethods =
            {
        ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6, ComputeItem7,
        ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12, ComputeItem13, ComputeItem14, ComputeItem15,
        ComputeItem16, ComputeItem17, ComputeItem18, ComputeItem19, ComputeItem20, ComputeItem21, ComputeItem22, ComputeItem23
    };

            foreach (var compute in computeMethods)
            {
                var item = compute?.Invoke();
                if (item != null)
                    list.Add(item);
            }

            // ✅ Append dynamic compute definitions (from API/disk store)
            AppendApiComputeItems(list);

            // ✅ Append admin "rate items" (from RateLibraryStore disk/cloud)
            AppendAdminRateItems(list);

            return list;
        }

        private void AppendAdminRateItems(List<FinishesItem> target)
        {
            var rates = RateLibraryStore.Items;
            if (rates == null || rates.Count == 0)
            {
                Debug.WriteLine($"[RateLibrary] No rate items in store. File={RateLibraryStore.FilePath}");
                return;
            }

            int nextNo = target.Count + 1;
            int appended = 0;

            Debug.WriteLine($"[RateLibrary] Store contains {rates.Count} total rates. Filtering section='{SectionKey}'");

            foreach (var r in rates)
            {
                if (r == null) continue;

                if (!string.Equals(r.SectionKey ?? "", SectionKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                var net = (double)r.NetCost;
                if (!(net > 0)) continue;

                var ohp = ApplyOHP(net);

                var breakdown = new ObservableCollection<FinishesBreakdownLine>();

                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        breakdown.Add(new FinishesBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice))
                        });
                    }
                }

                target.Add(new FinishesItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 0),
                    FinishesBreakdownLine = breakdown
                });

                appended++;
            }

            Debug.WriteLine($"[RateLibrary] Appended {appended} admin rate(s) for section '{SectionKey}'.");
        }



        private void OnLibraryChange()
        {
            RecomputeAll();
        }

        // ─────── FILTER – order by Net Cost (low → high) ──────
        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            FinishesCollectionView.SortDescriptions.Clear();

            if (_isNetCostFilterOn)
            {
                FinishesCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(FinishesItem.NetCost),
                        ListSortDirection.Ascending));
            }

            FinishesCollectionView?.Refresh();

        }

        // ─────── SORT – cycle → None ▪ Overhead ▪ Total Cost ──────
        private void CycleSort()
        {
            _currentSort = _currentSort switch
            {
                SortState.None => SortState.Overhead,
                SortState.Overhead => SortState.TotalCost,
                SortState.TotalCost => SortState.None,
                _ => SortState.None
            };

            FinishesCollectionView.SortDescriptions.Clear();

            switch (_currentSort)
            {
                case SortState.Overhead:
                    FinishesCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(FinishesItem.OverheadValue),
                            ListSortDirection.Ascending));
                    break;

                case SortState.TotalCost:
                    FinishesCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(FinishesItem.TotalCost),
                            ListSortDirection.Ascending));
                    break;

                case SortState.None:
                default:
                    break;
            }

            FinishesCollectionView?.Refresh();

        }

        private void OpenCustomRateEntry()
        {
            var view = new CustomRateEntryView();
            view.DataContext = new CustomRateEntryViewModel();
            SelectedDetail = view;
        }

        #endregion

        #region Core Compute + API Append

        //private void BuildFinishesItem()
        //{
        //    Func<FinishesItem>[] computeMethods =
        //    {
        //        ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,ComputeItem7,
        //        ComputeItem8,ComputeItem9,ComputeItem10,ComputeItem11, ComputeItem12,ComputeItem13,ComputeItem14,ComputeItem15,
        //        ComputeItem16,ComputeItem17,ComputeItem18, ComputeItem19,ComputeItem20,ComputeItem21,ComputeItem22,ComputeItem23
        //    };

        //    foreach (var compute in computeMethods)
        //    {
        //        var item = compute?.Invoke();
        //        if (item != null)
        //            FinishesItems.Add(item);
        //    }

        //    // ✅ Append dynamic compute definitions (from API/disk store)
        //    AppendApiComputeItems();
        //}

        private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
        {
            double ov = netCost * (OverheadPercent / 100);
            double pv = netCost * (ProfitPercent / 100);
            double total = netCost + ov + pv;

            return (ov, pv, total);
        }

        private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
        private double GetLabourRate(string name) => _helper.GetLabourRate(name);

        public double GetNetValue(Func<FinishesItem> computeItemFunc)
        {
            var item = computeItemFunc();
            return item.NetCost;
        }

        public double GetBlockworkNetValue(Func<BlockworkItem> computeFunc)
        {
            return _blockworkViewModel.GetNetValue(computeFunc);
        }

        private void AppendApiComputeItems(List<FinishesItem> target)
        {
            if (_computeEngine == null) return;

            var defs = ComputeCatalogStore.Items;
            if (defs == null || defs.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] No items loaded for section '{SectionKey}'.");
                return;
            }

            int appended = 0;
            int nextNo = target.Count + 1;

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

                    var breakdown = new ObservableCollection<FinishesBreakdownLine>();

                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new FinishesBreakdownLine
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
                        breakdown.Add(new FinishesBreakdownLine
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
                        breakdown.Add(new FinishesBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new FinishesBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    target.Add(new FinishesItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 0),
                        FinishesBreakdownLine = breakdown
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


        #endregion

        #region Compute Methods

        private FinishesItem ComputeItem1()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem2) *0.012;
            double mortarWastePer = 5;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 6;
            double labourQty = 6;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutPerDay = 50;
            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 12mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo= 1,
                Description = "Cement and sand (1:3) render to wall 12mm thick.",
                Unit="m2",
                NetCost= Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };

        }
        private FinishesItem ComputeItem2()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem5) * 0.012;
            double mortarWastePer = 5;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 6;
            double labourQty = 6;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutPerDay = 50;
            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 12mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 2,
                Description = "Water tight cement and sand (1:1) render to wall 12mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem3()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem3) * 0.012;
            double mortarWastePer = 5;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 6;
            double labourQty = 6;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutPerDay = 50;
            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 12mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 3,
                Description = "Cement and sand (1:4) render to wall 12mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem4()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem2) * 0.012;
            double mortarWastePer = 10;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 2;
            double labourQty = 2;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutHrPerM2 = 0.2;
            double outPutPerDay = Math.Round(1 / outPutHrPerM2 * 8, 2);

            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 15-19mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 4,
                Description = "Screeded Bed (1:3) 15-19mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem5()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem2) * 0.0245;
            double mortarWastePer = 10;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 2;
            double labourQty = 2;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutHrPerM2 = 0.25;
            double outPutPerDay = Math.Round(1 / outPutHrPerM2 * 8, 2);

            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 20-29mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 5,
                Description = "Screeded Bed (1:3) 20-29mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem6()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem2) * 0.0345;
            double mortarWastePer = 10;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 2;
            double labourQty = 2;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutHrPerM2 = 0.3;
            double outPutPerDay = Math.Round(1 / outPutHrPerM2 * 8, 2);

            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 30-39mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 6,
                Description = "Screeded Bed (1:3) 30-39mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem7()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem2) * 0.045;
            double mortarWastePer = 10;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 2;
            double labourQty = 2;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutHrPerM2 = 0.35;
            double outPutPerDay = Math.Round(1 / outPutHrPerM2 * 8, 2);

            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 40-50mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 7,
                Description = "Screeded Bed (1:3) 40-50mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem8()
        {
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem5) * 0.045;
            double mortarWastePer = 10;
            double mortarWaste = mortarCost * (mortarWastePer / 100);

            //lABOUR COST
            double headmanCost = (GetLabourRate("Headman")) * 1.4;
            double masonCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourCost = (GetLabourRate("Labourer")) * 1.4;

            double headmanQty = 1;
            double masonQty = 2;
            double labourQty = 2;

            double headmanRate = headmanCost * headmanQty;
            double masonRate = masonCost * masonQty;
            double labourRate = labourCost * labourQty;
            double totalLabourRate = headmanRate + masonRate + labourRate;

            double outPutHrPerM2 = 0.35;
            double outPutPerDay = Math.Round(1 / outPutHrPerM2 * 8, 2);

            double outputPerSqm = totalLabourRate / outPutPerDay;
            double netCostPerm2 = outputPerSqm + mortarCost + mortarWaste;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Mortar 40-50mm thick (See Blockwork)", Quantity=1, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarCost},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=mortarWastePer, Unit="%", UnitPrice=mortarWaste, TotalPrice=mortarWaste},

                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per/day", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Mason", Quantity=masonQty, Unit="per/day", UnitPrice=masonCost, TotalPrice=masonRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per/day", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Total Labour Per Day", TotalPrice=totalLabourRate},

                new FinishesBreakdownLine{ComponentName="Output per day", Quantity=outPutPerDay, Unit="m2/day", UnitPrice=totalLabourRate, TotalPrice=outputPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 8,
                Description = "Water tight screeded bed (1:1) 40-50mm thick.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem9()
        {
            #region Material Cost
            double whiteCementCost = GetMaterialPrice("White Cement");
            double whiteChippingCost = GetMaterialPrice("Terrazzo Chipping - White");
            double blackChippingCost = GetMaterialPrice("Terrazzo Chipping - Black");
            double unloadingBags = (GetLabourRate("Labourer")/8)*1.4;

            double whiteCementQty = 28;
            double whiteChippingQty = 70;
            double blackChippingQty = 70;
            double unloadingBagsDur = 2.57;

            double whiteCementRate = whiteCementCost * whiteCementQty;
            double whiteChippingRate = whiteChippingCost * whiteChippingQty;
            double blackChippingRate = blackChippingCost * blackChippingQty;
            double unloadingBagsRate = unloadingBags * unloadingBagsDur;

            double materialCostForSqm = whiteCementRate + whiteChippingRate + blackChippingRate + unloadingBagsRate;
            double materialCostPerSqm = materialCostForSqm/3.5;

            double shrinkagePer = 25;
            double shrinkage = materialCostPerSqm * (shrinkagePer / 100);
            double materialWithShrinkage = materialCostPerSqm + shrinkage;

            double wastagePer = 10;
            double wastage = materialWithShrinkage * (wastagePer / 100);
            #endregion

            #region Labour Cost
            //Mixing
            double mixingHeadmanCost = (GetLabourRate("Headman")/8)*1.4;
            double mixingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double mixingHeadmanQty = 1;
            double mixingLabourQty = 3;

            double mixingHeadmanRate = mixingHeadmanCost * mixingHeadmanQty;
            double mixingLabourRate = mixingLabourCost * mixingLabourQty;
            double totalMixingRate = mixingHeadmanRate + mixingLabourRate;

            //Placing
            double placingLabourCost = (GetLabourRate("Labourer") / 8)*1.4;
            double placingTilierCost = (GetLabourRate("Skilled/Artisan") / 8)*1.4;

            double placingLabourQty = 3;
            double placingTilierQty = 1;

            double placingLabourRate = placingLabourCost * placingLabourQty;
            double placingTilierRate = placingTilierCost * placingTilierQty;
            double totalPlacingRate = placingLabourRate + placingTilierRate;

            double totalMaterialCostPerCUM = materialWithShrinkage + wastage+totalMixingRate+totalPlacingRate;
            double pavingThickness = 0.019;
            double costPer19mmPaving = totalMaterialCostPerCUM * pavingThickness;
            #endregion

            #region Polishing
            //POLISHING COST
            double firstPolishCostPer20SqmQty = Math.Round(4.0 / 20.0, 2);
            double firstPolishGrindingMachine = Math.Round((5.0 / 60.0), 2);
            double firstPolishTerrazoMan = Math.Round((5.0 / 60.0), 2);
            double firstPolishTilingMan = Math.Round((5.0 / 60.0), 2);


            double firstPolishCostPer20SqmCost = GetMaterialPrice("Carborumdum stone (8 No. per set)");
            double firstPolishGrindingCost = GetLabourRate("Terrazzo Machine") / 8;
            double firstPolishTerrazoCost = (GetLabourRate("Skilled/Artisan")/8)*1.4;
            double firstPolishTilingCost = (GetLabourRate("Semi skilled")/8)*1.4;

            double firstPolishCostPer20SqmRate = firstPolishCostPer20SqmQty * firstPolishCostPer20SqmCost;
            double firstPolishGrindingRate = firstPolishGrindingMachine * firstPolishGrindingCost;
            double firstPolishTerrazoRate = firstPolishTerrazoMan * firstPolishTerrazoCost;
            double firstPolishTilingRate = firstPolishTilingMan * firstPolishTilingCost;

            double totalFirstPolish = firstPolishCostPer20SqmRate + firstPolishGrindingRate + firstPolishTerrazoRate + firstPolishTilingRate;

            double sodiumSillicatePer = 20;
            double secondPolishCostPer20SqmQty = Math.Round(4.0 / 20.0, 2);
            double secondPolishGrindingMachine = Math.Round(8.0 / 60.0, 2);
            double secondPolishTerrazoMan = Math.Round(8.0 / 60.0, 2);
            double secondPolishTilingMan = Math.Round(8.0 / 60.0, 2);

            double sodiumSillicateCost = GetMaterialPrice("Acid (Sodium Silicate Solution)");
            double secondPolishCostPer20SqmCost = GetMaterialPrice("Carborumdum stone (8 No. per set)");
            double secondPolishGrindingCost = GetLabourRate("Terrazzo Machine")/8;
            double secondPolishTerrazoCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double secondPolishTilingCost = (GetLabourRate("Semi skilled") / 8) * 1.4;

            double sodiumSillicateRate = sodiumSillicateCost * (sodiumSillicatePer / 100);
            double secondPolishCostPer20SqmRate = secondPolishCostPer20SqmQty * secondPolishCostPer20SqmCost;
            double secondPolishGrindingRate = secondPolishGrindingMachine * secondPolishGrindingCost;
            double secondPolishTerrazoRate = secondPolishTerrazoMan * secondPolishTerrazoCost;
            double secondPolishTilingRate = secondPolishTilingMan * secondPolishTilingCost;

            double totalSecondPolish = sodiumSillicateRate+ secondPolishCostPer20SqmRate + secondPolishGrindingRate + secondPolishTerrazoRate + secondPolishTilingRate;

            double waxPolishPer = 15;
            double finalPolishCostPer20SqmQty = Math.Round(4.0 / 20.0, 2);
            double finalPolishGrindingMachine = Math.Round(10.0 / 60.0, 2);
            double finalPolishTerrazoMan = Math.Round(10.0 / 60.0, 2);
            double finalPolishTilingMan = Math.Round(10.0 / 60.0, 2);

            double waxPolishCost = GetMaterialPrice("Wax polish");
            double finalPolishCostPer20SqmCost = GetMaterialPrice("Carborumdum stone (8 No. per set)");
            double finalPolishGrindingCost = GetLabourRate("Terrazzo Machine")/8;
            double finalPolishTerrazoCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double finalPolishTilingCost = (GetLabourRate("Semi skilled") / 8) * 1.4;

            double waxPolishRate = waxPolishCost * (waxPolishPer / 100);
            double finalPolishCostPer20SqmRate = finalPolishCostPer20SqmQty * finalPolishCostPer20SqmCost;
            double finalPolishGrindingRate = finalPolishGrindingMachine * finalPolishGrindingCost;
            double finalPolishTerrazoRate = finalPolishTerrazoMan * finalPolishTerrazoCost;
            double finalPolishTilingRate = finalPolishTilingMan * finalPolishTilingCost;

            double totalFinalPolish = waxPolishRate+ finalPolishCostPer20SqmRate + finalPolishGrindingRate + finalPolishTerrazoRate + finalPolishTilingRate;

            double totalPolish = totalFirstPolish + totalSecondPolish + totalFinalPolish;
            #endregion

            double netTerrazoPerSqm = costPer19mmPaving + totalPolish;

            var ohp = ApplyOHP(netTerrazoPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
				#region Material Cost
				new FinishesBreakdownLine{ ComponentName="White Cement", Quantity=whiteCementQty, Unit="bag", UnitPrice=whiteCementCost, TotalPrice=whiteCementRate},
                new FinishesBreakdownLine{ComponentName="White Chipping", Quantity=whiteChippingQty, Unit="bag", UnitPrice=whiteChippingCost, TotalPrice=whiteChippingRate},
                new FinishesBreakdownLine{ComponentName="Black Chipping", Quantity=blackChippingQty, Unit="bag", UnitPrice=blackChippingCost, TotalPrice=blackChippingRate},
                new FinishesBreakdownLine{ComponentName="Unloading Bags", Quantity=unloadingBagsDur, Unit="hrs/m3", UnitPrice=unloadingBags, TotalPrice=unloadingBagsRate},
                new FinishesBreakdownLine{ComponentName="Total material cost 3.5m2"  ,TotalPrice=materialCostForSqm},

                new FinishesBreakdownLine{ComponentName="Material cost for 1m2", Quantity=3.5, Unit="m2", TotalPrice=materialCostPerSqm},
                new FinishesBreakdownLine{ComponentName="Add shrinkage", Quantity=shrinkagePer, Unit="%", TotalPrice=shrinkage},
                new FinishesBreakdownLine{ComponentName="Sub-total", TotalPrice=materialWithShrinkage},
                new FinishesBreakdownLine{ComponentName="Add for waste and compaction", Quantity=wastagePer, Unit="%", TotalPrice=wastage},

                new FinishesBreakdownLine{ComponentName="Cost of plant and labour as before calculated.", Quantity=mixingHeadmanQty, Unit="m2/hr", UnitPrice=mixingHeadmanCost, TotalPrice=mixingHeadmanRate},
                new FinishesBreakdownLine{ComponentName="Mixing crew - labour.", Quantity=mixingLabourQty, Unit="per hr", UnitPrice=mixingLabourCost, TotalPrice=mixingLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Mixing", TotalPrice=totalMixingRate},

                new FinishesBreakdownLine{ComponentName="Placing crew - labour.", Quantity=placingLabourQty, Unit="per hr", UnitPrice=placingLabourCost, TotalPrice=placingLabourRate},
                new FinishesBreakdownLine{ComponentName="Placing crew - tiler.", Quantity=placingTilierQty, Unit="per hr", UnitPrice=placingTilierCost, TotalPrice=placingTilierRate},
                new FinishesBreakdownLine{ComponentName="Total Placing", TotalPrice=totalPlacingRate},

                new FinishesBreakdownLine{ComponentName="Total material cost per CUM", TotalPrice=totalMaterialCostPerCUM},
                new FinishesBreakdownLine{ComponentName="Cost per 19mm paving", Quantity=pavingThickness, Unit="m", TotalPrice=costPer19mmPaving},
				#endregion
				
				new FinishesBreakdownLine{ComponentName="4 sets per 20m2", Quantity=firstPolishCostPer20SqmQty, Unit="stone/m2", UnitPrice=firstPolishCostPer20SqmCost, TotalPrice=firstPolishCostPer20SqmRate},
                new FinishesBreakdownLine{ComponentName="Grinding machine", Quantity=firstPolishGrindingMachine, Unit="hrs/m2", UnitPrice=firstPolishGrindingCost, TotalPrice=firstPolishGrindingRate},
                new FinishesBreakdownLine{ComponentName="Tiller/Terrazzo Man", Quantity=firstPolishTerrazoMan, Unit="hrs/m2", UnitPrice=firstPolishTerrazoCost, TotalPrice=firstPolishTerrazoRate},
                new FinishesBreakdownLine{ComponentName="Tiling Assistant", Quantity=firstPolishTilingMan, Unit="hrs/m2", UnitPrice=firstPolishTilingCost, TotalPrice= firstPolishTilingRate},
                new FinishesBreakdownLine{ComponentName="Total First Polish", TotalPrice=totalFirstPolish},

                new FinishesBreakdownLine{ComponentName="20% Sodium Sillicate", Quantity=sodiumSillicatePer, Unit="%", UnitPrice=sodiumSillicateCost, TotalPrice=sodiumSillicateRate},
                new FinishesBreakdownLine{ComponentName="4 sets per 20m2", Quantity=secondPolishCostPer20SqmQty, Unit="stone/m2", UnitPrice=secondPolishCostPer20SqmCost, TotalPrice=secondPolishCostPer20SqmRate},
                new FinishesBreakdownLine{ComponentName="Grinding machine", Quantity=secondPolishGrindingMachine, Unit="hrs/m2", UnitPrice=secondPolishGrindingCost, TotalPrice=secondPolishGrindingRate},
                new FinishesBreakdownLine{ComponentName="Tiller/Terrazzo Man", Quantity=secondPolishTerrazoMan, Unit="hrs/m2", UnitPrice=secondPolishTerrazoCost, TotalPrice=secondPolishTerrazoRate},
                new FinishesBreakdownLine{ComponentName="Tiling Assistant", Quantity=secondPolishTilingMan, Unit="hrs/m2", UnitPrice=secondPolishTilingCost, TotalPrice=secondPolishTilingRate},
                new FinishesBreakdownLine{ComponentName="Total Second Polish", TotalPrice=totalSecondPolish},

                new FinishesBreakdownLine{ComponentName="15% Wax Polish", Quantity=waxPolishPer, Unit="%", UnitPrice=waxPolishCost, TotalPrice=waxPolishRate},
                new FinishesBreakdownLine{ComponentName="4 sets per 20m2", Quantity=finalPolishCostPer20SqmQty, Unit="stone/m2", UnitPrice=finalPolishCostPer20SqmCost, TotalPrice=finalPolishCostPer20SqmRate},
                new FinishesBreakdownLine{ComponentName="Grinding machine", Quantity=finalPolishGrindingMachine, Unit="hrs/m2", UnitPrice=finalPolishGrindingCost, TotalPrice=finalPolishGrindingRate},
                new FinishesBreakdownLine{ComponentName="Tiller/Terrazzo Man", Quantity=finalPolishTerrazoMan, Unit="hrs/m2", UnitPrice=finalPolishTerrazoCost, TotalPrice=finalPolishTerrazoRate},
                new FinishesBreakdownLine{ComponentName="Tiling Assistant", Quantity=finalPolishTilingMan, Unit="hrs/m2", UnitPrice=finalPolishTilingCost, TotalPrice=finalPolishTilingRate},
                new FinishesBreakdownLine{ComponentName="Total Final Polish", TotalPrice=totalFinalPolish},

                new FinishesBreakdownLine{ComponentName="Total Polish", TotalPrice=totalPolish},

                new FinishesBreakdownLine{ComponentName="Net Cost per m2", TotalPrice=netTerrazoPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 9,
                Description = "19mm thick Terrazzo laid with white cement, in bays with black ebonite dividing strips on screeded bed (measured separately)",
                Unit = "m2",
                NetCost = Math.Round(netTerrazoPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem10()
        {
            double flexTileCost = GetMaterialPrice("Floor tiles 300 x 300, 56 tiles per carton")/5.04;
            double flexTileWastePer = 10;
            double flexTileWaste = flexTileCost * (flexTileWastePer / 100);
            double totalFlexTilesWaste = flexTileWaste * (flexTileWastePer / 100);
            double adhesiveCost = (GetMaterialPrice("Florflex adhesive (4 tins per carton)")/4.0)/17.5;

            double flexTileQty = 1;
            double adhesiveQty = 1;

            double flexTileRate = flexTileCost * flexTileQty;
            double adhesiveRate = adhesiveCost * adhesiveQty;

            double totalMaterialRate = flexTileRate + adhesiveRate + totalFlexTilesWaste;

            //LABOUR COST
            double tilesLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;

            double tilesLabourQty = 12.0/60.0;
            double labourQty = 12.0/60.0;
            double headmanQty = 5.0/60.0;

            double tilesLabourRate = tilesLabourCost * tilesLabourQty;
            double labourRate = labourCost * labourQty;
            double headmanRate = headmanCost * headmanQty;

            double totalLabourRate = tilesLabourRate + labourRate + headmanRate;

            double netCostPerm2 = totalMaterialRate + totalLabourRate;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "1.3mm floor flex tile", Quantity=flexTileQty, Unit="m2", UnitPrice=flexTileCost, TotalPrice=flexTileRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=flexTileWastePer, Unit="%", UnitPrice=flexTileWaste, TotalPrice=totalFlexTilesWaste},
                new FinishesBreakdownLine{ ComponentName="Adhesive", Quantity=adhesiveQty, Unit="m2", UnitPrice=adhesiveCost, TotalPrice=adhesiveRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Laying tiles using 1 tiler.", Quantity=tilesLabourQty, Unit="per hr", UnitPrice=tilesLabourCost, TotalPrice=tilesLabourRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="per hr", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="per hr", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 10,
                Description = "1.3mm thick Nigerite PVC Floorflex tile (56 no. pack) fixed on smooth (timber or concrete) surface  with  floorflex  adhesive.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem11()
        {
            double flexTileCost = GetMaterialPrice("300 x 300 x 1.3mm thick floor tiles (42 tiles per carton)") / 5.04;
            double flexTileWastePer = 10;
            double flexTileWaste = flexTileCost * (flexTileWastePer / 100);
            double totalFlexTilesWaste = flexTileWaste * (flexTileWastePer / 100);

            double adhesiveCost = (GetMaterialPrice("Florflex adhesive (4 tins per carton)") / 4.0) / 17.5;

            double flexTileQty = 1;
            double adhesiveQty = 1;

            double flexTileRate = flexTileCost * flexTileQty;
            double adhesiveRate = adhesiveCost * adhesiveQty;

            double totalMaterialRate = flexTileRate + adhesiveRate + totalFlexTilesWaste;

            //LABOUR COST
            double tilesLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;

            double tilesLabourQty = 12.0 / 60.0;
            double labourQty = 12.0 / 60.0;
            double headmanQty = 5.0 / 60.0;

            double tilesLabourRate = tilesLabourCost * tilesLabourQty;
            double labourRate = labourCost * labourQty;
            double headmanRate = headmanCost * headmanQty;

            double totalLabourRate = tilesLabourRate + labourRate + headmanRate;

            double netCostPerm2 = totalMaterialRate + totalLabourRate;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "1.3mm floor flex tile", Quantity=flexTileQty, Unit="m2", UnitPrice=flexTileCost, TotalPrice=flexTileRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=flexTileWastePer, Unit="%", UnitPrice=flexTileWaste, TotalPrice=totalFlexTilesWaste},
                new FinishesBreakdownLine{ ComponentName="Adhesive", Quantity=adhesiveQty, Unit="m2", UnitPrice=adhesiveCost, TotalPrice=adhesiveRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Laying tiles using 1 tiler.", Quantity=tilesLabourQty, Unit="hr/m2", UnitPrice=tilesLabourCost, TotalPrice=tilesLabourRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="hr/m2", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 11,
                Description = "1.3mm thick Nigerite PVC Floorflex tile (42 no. pack) fixed on smooth (timber or concrete) surface  with  floorflex  adhesive.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem12()
        {
            double flexTileCost = GetMaterialPrice("Floor tiles 1.6mm thick, 36 tiles per carton") / 3.24;
            double flexTileWastePer = 10;
            double flexTileWaste = flexTileCost * (flexTileWastePer / 100);
            double totalFlexTilesWaste = flexTileWaste * (flexTileWastePer / 100);

            double adhesiveCost = (GetMaterialPrice("Florflex adhesive (4 tins per carton)") / 4.0) / 17.5;

            double flexTileQty = 1;
            double adhesiveQty = 1;

            double flexTileRate = flexTileCost * flexTileQty;
            double adhesiveRate = adhesiveCost * adhesiveQty;

            double totalMaterialRate = flexTileRate + adhesiveRate + totalFlexTilesWaste;

            //LABOUR COST
            double tilesLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;

            double tilesLabourQty = 12.0 / 60.0;
            double labourQty = 12.0 / 60.0;
            double headmanQty = 5.0 / 60.0;

            double tilesLabourRate = tilesLabourCost * tilesLabourQty;
            double labourRate = labourCost * labourQty;
            double headmanRate = headmanCost * headmanQty;

            double totalLabourRate = tilesLabourRate + labourRate + headmanRate;

            double netCostPerm2 = totalMaterialRate + totalLabourRate;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "1.6mm floor flex tile", Quantity=flexTileQty, Unit="m2", UnitPrice=flexTileCost, TotalPrice=flexTileRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=flexTileWastePer, Unit="%", UnitPrice=flexTileWaste, TotalPrice=totalFlexTilesWaste},
                new FinishesBreakdownLine{ ComponentName="Adhesive", Quantity=adhesiveQty, Unit="m2", UnitPrice=adhesiveCost, TotalPrice=adhesiveRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Laying tiles using 1 tiler.", Quantity=tilesLabourQty, Unit="hr/m2", UnitPrice=tilesLabourCost, TotalPrice=tilesLabourRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="hr/m2", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 12,
                Description = "1.6mm thick Nigerite PVC Floorflex tile (36 no. pack) fixed on smooth (timber or concrete) surface  with  floorflex  adhesive.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem13()
        {
            double flexTileCost = GetMaterialPrice("Floor tiles 56 tiles per carton") / 5.04;
            double flexTileWastePer = 10;
            double flexTileWaste = flexTileCost * (flexTileWastePer / 100);
            double totalFlexTilesWaste = flexTileWaste * (flexTileWastePer / 100);

            double adhesiveCost = (GetMaterialPrice("Florflex adhesive (4 tins per carton)") / 4.0) / 17.5;

            double flexTileQty = 1;
            double adhesiveQty = 1;

            double flexTileRate = flexTileCost * flexTileQty;
            double adhesiveRate = adhesiveCost * adhesiveQty;

            double totalMaterialRate = flexTileRate + adhesiveRate + totalFlexTilesWaste;

            //LABOUR COST
            double tilesLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;

            double tilesLabourQty = 12.0 / 60.0;
            double labourQty = 12.0 / 60.0;
            double headmanQty = 5.0 / 60.0;

            double tilesLabourRate = tilesLabourCost * tilesLabourQty;
            double labourRate = labourCost * labourQty;
            double headmanRate = headmanCost * headmanQty;

            double totalLabourRate = tilesLabourRate + labourRate + headmanRate;

            double netCostPerm2 = totalMaterialRate + totalLabourRate;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "1.6mm floor flex tile", Quantity=flexTileQty, Unit="m2", UnitPrice=flexTileCost, TotalPrice=flexTileRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=flexTileWastePer, Unit="%", UnitPrice=flexTileWaste, TotalPrice=totalFlexTilesWaste},
                new FinishesBreakdownLine{ ComponentName="Adhesive", Quantity=adhesiveQty, Unit="m2", UnitPrice=adhesiveCost, TotalPrice=adhesiveRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Laying tiles using 1 tiler.", Quantity=tilesLabourQty, Unit="hr/m2", UnitPrice=tilesLabourCost, TotalPrice=tilesLabourRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="hr/m2", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 13,
                Description = "1.6mm thick Nigerite PVC Floorflex tile (56 no. pack) fixed on smooth (timber or concrete) surface  with  floorflex  adhesive.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem14()
        {
            double flexTileCost = GetMaterialPrice("Floor tiles 2.0mm thick (56 tiles per carton)") / 5.04;
            double flexTileWastePer = 10;
            double flexTileWaste = flexTileCost * (flexTileWastePer / 100);
            double totalFlexTilesWaste = flexTileWaste * (flexTileWastePer / 100);

            double adhesiveCost = (GetMaterialPrice("Florflex adhesive (4 tins per carton)") / 4.0) / 17.5;

            double flexTileQty = 1;
            double adhesiveQty = 1;

            double flexTileRate = flexTileCost * flexTileQty;
            double adhesiveRate = adhesiveCost * adhesiveQty;

            double totalMaterialRate = flexTileRate + adhesiveRate + totalFlexTilesWaste;

            //LABOUR COST
            double tilesLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;

            double tilesLabourQty = 12.0 / 60.0;
            double labourQty = 12.0 / 60.0;
            double headmanQty = 5.0 / 60.0;

            double tilesLabourRate = tilesLabourCost * tilesLabourQty;
            double labourRate = labourCost * labourQty;
            double headmanRate = headmanCost * headmanQty;

            double totalLabourRate = tilesLabourRate + labourRate + headmanRate;

            double netCostPerm2 = totalMaterialRate + totalLabourRate;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "2.0mm floor flex tile", Quantity=flexTileQty, Unit="m2", UnitPrice=flexTileCost, TotalPrice=flexTileRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=flexTileWastePer, Unit="%", UnitPrice=flexTileWaste, TotalPrice=totalFlexTilesWaste},
                new FinishesBreakdownLine{ ComponentName="Adhesive", Quantity=adhesiveQty, Unit="m2", UnitPrice=adhesiveCost, TotalPrice=adhesiveRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Laying tiles using 1 tiler.", Quantity=tilesLabourQty, Unit="hr/m2", UnitPrice=tilesLabourCost, TotalPrice=tilesLabourRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="hr/m2", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 14,
                Description = "2.0mm thick Nigerite PVC Floorflex tile (56 no. pack) fixed on smooth (timber or concrete) surface  with  floorflex  adhesive.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem15()
        {
            double flexTileCost = GetMaterialPrice("Floor tiles 3.0mm thick (38 tiles per carton)") / 5.04;
            double flexTileWastePer = 10;
            double flexTileWaste = flexTileCost * (flexTileWastePer / 100);
            double totalFlexTilesWaste = flexTileWaste * (flexTileWastePer / 100);

            double adhesiveCost = (GetMaterialPrice("Florflex adhesive (4 tins per carton)") / 4.0) / 17.5;

            double flexTileQty = 1;
            double adhesiveQty = 1;

            double flexTileRate = flexTileCost * flexTileQty;
            double adhesiveRate = adhesiveCost * adhesiveQty;

            double totalMaterialRate = flexTileRate + adhesiveRate + totalFlexTilesWaste;

            //LABOUR COST
            double tilesLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;

            double tilesLabourQty = 12.0 / 60.0;
            double labourQty = 12.0 / 60.0;
            double headmanQty = 5.0 / 60.0;

            double tilesLabourRate = tilesLabourCost * tilesLabourQty;
            double labourRate = labourCost * labourQty;
            double headmanRate = headmanCost * headmanQty;

            double totalLabourRate = tilesLabourRate + labourRate + headmanRate;

            double netCostPerm2 = totalMaterialRate + totalLabourRate;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "3.0mm floor flex tile", Quantity=flexTileQty, Unit="m2", UnitPrice=flexTileCost, TotalPrice=flexTileRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=flexTileWastePer, Unit="%", UnitPrice=flexTileWaste, TotalPrice=totalFlexTilesWaste},
                new FinishesBreakdownLine{ ComponentName="Adhesive", Quantity=adhesiveQty, Unit="m2", UnitPrice=adhesiveCost, TotalPrice=adhesiveRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Laying tiles using 1 tiler.", Quantity=tilesLabourQty, Unit="hr/m2", UnitPrice=tilesLabourCost, TotalPrice=tilesLabourRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourQty, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="hr/m2", UnitPrice=headmanCost, TotalPrice=headmanRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Total Cost per m2", TotalPrice=netCostPerm2},
            };

            return new FinishesItem
            {
                ItemNo = 15,
                Description = "3.0mm thick Nigerite PVC Floorflex tile (38 no. pack) fixed on smooth (timber or concrete) surface  with  floorflex  adhesive.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem16()
        {
            double ceilingCost = GetMaterialPrice("84R with open sides");
            double ceilingQty = 1;
            double ceilingRate = ceilingCost * ceilingQty;
            double wastePer = 5;
            double waste = ceilingRate * (wastePer / 100);
            double accessoriesPer = 7.5;
            double accessories = ceilingRate * (accessoriesPer / 100);
            double totalCeiling = ceilingRate + waste + accessories;

            //LABOUR COST
            double ceilingForemanCost = (GetLabourRate("Foreman")) * 1.4;
            double ceilingFitterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double ceilingLabourerCost = (GetLabourRate("Labourer")) * 1.4;

            double ceilingForemanQty = 1;
            double ceilingFitterQty = 2;
            double ceilingLabourerQty = 1;

            double ceilingForemanRate = ceilingForemanCost * ceilingForemanQty;
            double ceilingFitterRate = ceilingFitterCost * ceilingFitterQty;
            double ceilingLabourerRate = ceilingLabourerCost * ceilingLabourerQty;

            double totalLabourRate = ceilingForemanRate + ceilingFitterRate + ceilingLabourerRate;
            double dailyOutput = 25;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalCeiling + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Luxalon ceiling type 84R", Quantity=ceilingQty, Unit="m2", UnitPrice=ceilingCost, TotalPrice=ceilingRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new FinishesBreakdownLine{ ComponentName="Accessories", Quantity=accessoriesPer, Unit="%", TotalPrice=accessories},
                new FinishesBreakdownLine{ ComponentName="Total Ceiling", TotalPrice=totalCeiling},
                new FinishesBreakdownLine{ComponentName="Foreman", Quantity=ceilingForemanQty, Unit="per day", UnitPrice=ceilingForemanCost, TotalPrice=ceilingForemanRate},
                new FinishesBreakdownLine{ComponentName="Fitter", Quantity=ceilingFitterQty, Unit="per day", UnitPrice=ceilingFitterCost, TotalPrice=ceilingFitterRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=ceilingLabourerQty, Unit="per day", UnitPrice=ceilingLabourerCost, TotalPrice=ceilingLabourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 16,
                Description = "Procure and install luxalon ceiling type 84R self lock 84mm wide x 6000mm long complete. with hangers at height not exceeding 3.00m from ground level.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem17()
        {
            double ceilingCost = GetMaterialPrice("2'x2' (600x600x13-15mm) Celotex acoustic ceiling tile (16 tiles per box) complete with aluminium suspension grid")/5.76;
            double ceilingQty = 1;
            double ceilingRate = ceilingCost * ceilingQty;
            double wastePer = 5;
            double waste = ceilingRate * (wastePer / 100);
            double accessoriesPer = 5;
            double accessories = ceilingRate * (accessoriesPer / 100);
            double totalCeiling = ceilingRate + waste + accessories;

            //LABOUR COST
            double ceilingForemanCost = (GetLabourRate("Foreman")) * 1.4;
            double ceilingFitterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double ceilingLabourerCost = (GetLabourRate("Labourer")) * 1.4;

            double ceilingForemanQty = 1;
            double ceilingFitterQty = 2;
            double ceilingLabourerQty = 1;

            double ceilingForemanRate = ceilingForemanCost * ceilingForemanQty;
            double ceilingFitterRate = ceilingFitterCost * ceilingFitterQty;
            double ceilingLabourerRate = ceilingLabourerCost * ceilingLabourerQty;

            double totalLabourRate = ceilingForemanRate + ceilingFitterRate + ceilingLabourerRate;
            double dailyOutput = 30;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalCeiling + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Celotex ceiling.", Quantity=ceilingQty, Unit="m2", UnitPrice=ceilingCost, TotalPrice=ceilingRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new FinishesBreakdownLine{ ComponentName="Accessories", Quantity=accessoriesPer, Unit="%", TotalPrice=accessories},
                new FinishesBreakdownLine{ ComponentName="Total Ceiling", TotalPrice=totalCeiling},
                new FinishesBreakdownLine{ComponentName="Foreman", Quantity=ceilingForemanQty, Unit="per day", UnitPrice=ceilingForemanCost, TotalPrice=ceilingForemanRate},
                new FinishesBreakdownLine{ComponentName="Fitter", Quantity=ceilingFitterQty, Unit="per day", UnitPrice=ceilingFitterCost, TotalPrice=ceilingFitterRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=ceilingLabourerQty, Unit="per day", UnitPrice=ceilingLabourerCost, TotalPrice=ceilingLabourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 17,
                Description = "Procure and install 600 x 600 x 13mm suspended celotex ceiling fixed in exposed aluminium grid at height not exceeding 3.00m form ground level.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem18()
        {
            double ceilingCost = GetMaterialPrice("4'x4' (1200x1200mm x 3mm thick) Asbestos ceiling sheets") / 0.44;
            double ceilingQty = 1;
            double ceilingRate = ceilingCost * ceilingQty;
            double wastePer = 5;
            double waste = ceilingRate * (wastePer / 100);
            double accessoriesPer = 5;
            double accessories = ceilingRate * (accessoriesPer / 100);
            double totalCeiling = ceilingRate + waste + accessories;

            //LABOUR COST
            //double ceilingForemanCost = (GetLabourRate("Foreman")) * 1.4;
            double ceilingFitterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double ceilingLabourerCost = (GetLabourRate("Labourer")) * 1.4;

            //double ceilingForemanQty = 1;
            double ceilingFitterQty = 1;
            double ceilingLabourerQty = 1;

            //double ceilingForemanRate = ceilingForemanCost * ceilingForemanQty;
            double ceilingFitterRate = ceilingFitterCost * ceilingFitterQty;
            double ceilingLabourerRate = ceilingLabourerCost * ceilingLabourerQty;

            double totalLabourRate = ceilingFitterRate + ceilingLabourerRate;
            double dailyOutput = 40;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalCeiling + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Asbestos ceiling.", Quantity=ceilingQty, Unit="m2", UnitPrice=ceilingCost, TotalPrice=ceilingRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new FinishesBreakdownLine{ ComponentName="Accessories", Quantity=accessoriesPer, Unit="%", TotalPrice=accessories},
                new FinishesBreakdownLine{ ComponentName="Total Ceiling", TotalPrice=totalCeiling},
				//new FinishesBreakdownLine{ComponentName="Foreman", Quantity=ceilingForemanQty, Unit="per day", UnitPrice=ceilingForemanCost, TotalPrice=ceilingForemanRate},
				new FinishesBreakdownLine{ComponentName="Carpenters", Quantity=ceilingFitterQty, Unit="per day", UnitPrice=ceilingFitterCost, TotalPrice=ceilingFitterRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=ceilingLabourerQty, Unit="per day", UnitPrice=ceilingLabourerCost, TotalPrice=ceilingLabourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 18,
                Description = "Procure and install 1200 x 1200 x 3mm asbestos ceiling boards fixed to timber soffit (Battens measured seperately).",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem19()
        {
            double ceilingCost = GetMaterialPrice("2'x2' (610x610x4mm) Decoceil decorative sheet (16 tiles per box)") / 5.95;
            double ceilingQty = 1;
            double ceilingRate = ceilingCost * ceilingQty;
            double wastePer = 5;
            double waste = ceilingRate * (wastePer / 100);
            double accessoriesPer = 5;
            double accessories = ceilingRate * (accessoriesPer / 100);
            double totalCeiling = ceilingRate + waste + accessories;

            //LABOUR COST
            //double ceilingForemanCost = (GetLabourRate("Foreman")) * 1.4;
            double ceilingFitterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double ceilingLabourerCost = (GetLabourRate("Labourer")) * 1.4;

            //double ceilingForemanQty = 1;
            double ceilingFitterQty = 1;
            double ceilingLabourerQty = 1;

            //double ceilingForemanRate = ceilingForemanCost * ceilingForemanQty;
            double ceilingFitterRate = ceilingFitterCost * ceilingFitterQty;
            double ceilingLabourerRate = ceilingLabourerCost * ceilingLabourerQty;

            double totalLabourRate = ceilingFitterRate + ceilingLabourerRate;
            double dailyOutput = 30;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalCeiling + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Asbestos ceiling.", Quantity=ceilingQty, Unit="m2", UnitPrice=ceilingCost, TotalPrice=ceilingRate},
                new FinishesBreakdownLine{ ComponentName="Add waste", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new FinishesBreakdownLine{ ComponentName="Accessories", Quantity=accessoriesPer, Unit="%", TotalPrice=accessories},
                new FinishesBreakdownLine{ ComponentName="Total Ceiling", TotalPrice=totalCeiling},
				//new FinishesBreakdownLine{ComponentName="Foreman", Quantity=ceilingForemanQty, Unit="per day", UnitPrice=ceilingForemanCost, TotalPrice=ceilingForemanRate},
				new FinishesBreakdownLine{ComponentName="Carpenters", Quantity=ceilingFitterQty, Unit="per day", UnitPrice=ceilingFitterCost, TotalPrice=ceilingFitterRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=ceilingLabourerQty, Unit="per day", UnitPrice=ceilingLabourerCost, TotalPrice=ceilingLabourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 19,
                Description = "Procure and install 610 x 610 x 4mm 'decoceil' asbestos ceiling boards fixed to timber soffit (Battens measured seperately).",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem20()
        {
            double tileCost = GetMaterialPrice("12x12\" (300x300mm) Brazilian Floor Tiles");
            double transportPer = 5;
            double transport = tileCost * (transportPer / 100);
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem4);

            double tileQty = 1;
            double mortarQty = 0.015;

            double tileRate = tileCost * tileQty;
            double mortarRate = mortarCost * mortarQty;

            double totalMaterialRate = tileRate + transport + mortarRate;

            //LABOUR COST
            double tilesHeadmanCost = (GetLabourRate("Headman")) * 1.4;
            double tilerCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourerCost = (GetLabourRate("Labourer")) * 1.4;

            double tilesHeadmanQty = 1;
            double tilerQty = 1;
            double labourerQty = 1;

            double tilesHeadmanRate = tilesHeadmanCost * tilesHeadmanQty;
            double tilerRate = tilerCost * tilerQty;
            double labourerRate = labourerCost * labourerQty;

            double totalLabourRate = tilesHeadmanRate + tilerRate + labourerRate;
            double dailyOutput = 20;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalMaterialRate + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Vitrified floor tiles size 300 x 300 x 10mm", Quantity=tileQty, Unit="m2", UnitPrice=tileCost, TotalPrice=tileRate},
                new FinishesBreakdownLine{ ComponentName="Transport", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new FinishesBreakdownLine{ ComponentName="Mortar", Quantity=mortarQty, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=tilesHeadmanQty, Unit="per/day", UnitPrice=tilesHeadmanCost, TotalPrice=tilesHeadmanRate},
                new FinishesBreakdownLine{ComponentName="Tiler", Quantity=tilerQty, Unit="per/day", UnitPrice=tilerCost, TotalPrice=tilerRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourerQty, Unit="per/day", UnitPrice=labourerCost, TotalPrice=labourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 20,
                Description = "Procure and install 300 x 300 x 10mm vitrified floor tiles on screeded bed. (Screeded bed not included in rate)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem21()
        {
            double tileCost = GetMaterialPrice("8x8\" (200x200mm) Italian Floor Tiles");
            double transportPer = 5;
            double transport = tileCost * (transportPer / 100);
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem4);

            double tileQty = 1;
            double mortarQty = 0.015;

            double tileRate = tileCost * tileQty;
            double mortarRate = mortarCost * mortarQty;

            double totalMaterialRate = tileRate + transport + mortarRate;

            //LABOUR COST
            double tilesHeadmanCost = (GetLabourRate("Headman")) * 1.4;
            double tilerCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourerCost = (GetLabourRate("Labourer")) * 1.4;

            double tilesHeadmanQty = 1;
            double tilerQty = 1;
            double labourerQty = 1;

            double tilesHeadmanRate = tilesHeadmanCost * tilesHeadmanQty;
            double tilerRate = tilerCost * tilerQty;
            double labourerRate = labourerCost * labourerQty;

            double totalLabourRate = tilesHeadmanRate + tilerRate + labourerRate;
            double dailyOutput = 20;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalMaterialRate + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Vitrified floor tiles size 200 x 200 x 10mm", Quantity=tileQty, Unit="m2", UnitPrice=tileCost, TotalPrice=tileRate},
                new FinishesBreakdownLine{ ComponentName="Transport", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new FinishesBreakdownLine{ ComponentName="Mortar", Quantity=mortarQty, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=tilesHeadmanQty, Unit="per/day", UnitPrice=tilesHeadmanCost, TotalPrice=tilesHeadmanRate},
                new FinishesBreakdownLine{ComponentName="Tiler", Quantity=tilerQty, Unit="per/day", UnitPrice=tilerCost, TotalPrice=tilerRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourerQty, Unit="per/day", UnitPrice=labourerCost, TotalPrice=labourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 21,
                Description = "Procure and install 200 x 200 x 10mm vitrified floor tiles on screeded bed. (Screeded bed not included in rate)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem22()
        {
            double tileCost = GetMaterialPrice("6x6\" (150x150mm) - Royal Floor Tiles")/2;
            double transportPer = 5;
            double transport = tileCost * (transportPer / 100);
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem4);

            double tileQty = 1;
            double mortarQty = 0.015;

            double tileRate = tileCost * tileQty;
            double mortarRate = mortarCost * mortarQty;

            double totalMaterialRate = tileRate + transport + mortarRate;

            //LABOUR COST
            double tilesHeadmanCost = (GetLabourRate("Headman")) * 1.4;
            double tilerCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourerCost = (GetLabourRate("Labourer")) * 1.4;

            double tilesHeadmanQty = 1;
            double tilerQty = 1;
            double labourerQty = 1;

            double tilesHeadmanRate = tilesHeadmanCost * tilesHeadmanQty;
            double tilerRate = tilerCost * tilerQty;
            double labourerRate = labourerCost * labourerQty;

            double totalLabourRate = tilesHeadmanRate + tilerRate + labourerRate;
            double dailyOutput = 20;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalMaterialRate + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Ceramic Wall tiles size 150 x 150 x 6mm (Nigerian - Royal)", Quantity=tileQty, Unit="m2", UnitPrice=tileCost, TotalPrice=tileRate},
                new FinishesBreakdownLine{ ComponentName="Transport", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new FinishesBreakdownLine{ ComponentName="Mortar", Quantity=mortarQty, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=tilesHeadmanQty, Unit="per/day", UnitPrice=tilesHeadmanCost, TotalPrice=tilesHeadmanRate},
                new FinishesBreakdownLine{ComponentName="Tiler", Quantity=tilerQty, Unit="per/day", UnitPrice=tilerCost, TotalPrice=tilerRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourerQty, Unit="per/day", UnitPrice=labourerCost, TotalPrice=labourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 22,
                Description = "Procure and install local maunfactured 150 x 150 x 6mm ceramic wall tiles on screeded backing. (Backing not included in rate)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }
        private FinishesItem ComputeItem23()
        {
            double tileCost = GetMaterialPrice("Italian Wall Tiles (6x6\")");
            double transportPer = 5;
            double transport = tileCost * (transportPer / 100);
            double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem4);

            double tileQty = 1;
            double mortarQty = 0.015;

            double tileRate = tileCost * tileQty;
            double mortarRate = mortarCost * mortarQty;

            double totalMaterialRate = tileRate + transport + mortarRate;

            //LABOUR COST
            double tilesHeadmanCost = (GetLabourRate("Headman")) * 1.4;
            double tilerCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double labourerCost = (GetLabourRate("Labourer")) * 1.4;

            double tilesHeadmanQty = 1;
            double tilerQty = 1;
            double labourerQty = 1;

            double tilesHeadmanRate = tilesHeadmanCost * tilesHeadmanQty;
            double tilerRate = tilerCost * tilerQty;
            double labourerRate = labourerCost * labourerQty;

            double totalLabourRate = tilesHeadmanRate + tilerRate + labourerRate;
            double dailyOutput = 20;
            double labourPerSqm = totalLabourRate / dailyOutput;

            double netCostPerSqm = totalMaterialRate + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<FinishesBreakdownLine>
            {
                new FinishesBreakdownLine{ ComponentName= "Ceramic Wall tiles size 150 x 150 x 6mm (Italian)", Quantity=tileQty, Unit="m2", UnitPrice=tileCost, TotalPrice=tileRate},
                new FinishesBreakdownLine{ ComponentName="Transport", Quantity=transportPer, Unit="%", TotalPrice=transport},
                new FinishesBreakdownLine{ ComponentName="Mortar", Quantity=mortarQty, Unit="m2", UnitPrice=mortarCost, TotalPrice=mortarRate},
                new FinishesBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialRate},
                new FinishesBreakdownLine{ComponentName="Headman", Quantity=tilesHeadmanQty, Unit="per/day", UnitPrice=tilesHeadmanCost, TotalPrice=tilesHeadmanRate},
                new FinishesBreakdownLine{ComponentName="Tiler", Quantity=tilerQty, Unit="per/day", UnitPrice=tilerCost, TotalPrice=tilerRate},
                new FinishesBreakdownLine{ComponentName="Labourer", Quantity=labourerQty, Unit="per/day", UnitPrice=labourerCost, TotalPrice=labourerRate},
                new FinishesBreakdownLine{ComponentName="Total Labour", TotalPrice=totalLabourRate},
                new FinishesBreakdownLine{ComponentName="Labour per sqm", TotalPrice=labourPerSqm},
                new FinishesBreakdownLine{ComponentName="Total Cost per sqm", TotalPrice=netCostPerSqm},
            };

            return new FinishesItem
            {
                ItemNo = 23,
                Description = "Procure and install imported 150 x 150 x 6mm ceramic wall tiles on screeded backing. (Backing not included in rate)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 0),
                FinishesBreakdownLine = breakdown
            };
        }

        #endregion

    }
}
