using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;

namespace ADLMRateGen.ViewModel.RoofWork
{
    public class RoofWorkViewModel : ViewModelBase
    {
        private readonly GetItemsFromDB _helper;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        // ─── Sorting / filtering helpers ──────────────────────────────────────────────
        private bool _isNetCostFilterOn = false;
        private SortState _currentSort = SortState.None;
        private enum SortState { None, Overhead, TotalCost }

        private const string SectionKey = SectionKeys.Roofing; // ✅ Roofing section key
        private readonly ComputeItemEngine _computeEngine;

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

        public ObservableCollection<RoofWorkItem> RoofWorkItems { get; set; } =
            new ObservableCollection<RoofWorkItem>();

        public ICollectionView RoofworkCollectionView { get; private set; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    RaisePropertyChanged();
                    RoofworkCollectionView?.Refresh();
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

        public RoofWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);

            matLib.LibraryChanged += OnLibraryChanged;
            labourLib.LibraryChanged += OnLibraryChanged;

            // ✅ CollectionView first (so binding is stable)
            RoofworkCollectionView = CollectionViewSource.GetDefaultView(RoofWorkItems);
            RoofworkCollectionView.Filter = FilterRoofItem;

            RecomputeCommand = new DelegateCommand(_ => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
            FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
            SortCommand = new DelegateCommand(_ => CycleSort());
            AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

            // ✅ Compute Engine
            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);

            // ✅ DISK FIRST (offline-friendly)
            ComputeCatalogStore.ReloadFromDisk();
            RateLibraryStore.ReloadFromDisk();

            // ✅ Subscribe to store updates (API refresh completes later)
            ComputeCatalogStore.Changed += OnLibraryChanged;
            RateLibraryStore.Changed += OnLibraryChanged;

            // ✅ Start API refresh (async)
            _ = LoadComputeCatalogForSectionAsync();
            _ = LoadRateLibraryAsync();

            // ✅ Build initial items from local disk cache immediately
            RecomputeAll();

            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
                    RecomputeAll();
            };

            UserRateEditStore.Current.OverridesChanged += (_, __) =>
            {
                void Refresh()
                {
                    var nos = RoofWorkItems.Select(i => i.ItemNo).ToList();
                    foreach (var n in nos) RecomputeItemInPlace(n);
                }
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp == null || disp.CheckAccess()) Refresh();
                else disp.BeginInvoke((Action)Refresh);
            };
        }

        /* -------------------- API LOADERS -------------------- */

        private async Task LoadComputeCatalogForSectionAsync()
        {
            try
            {
                var ok = await ComputeCatalogStore.RefreshFromApiAsync(SectionKey);

                // fallback if section mismatch
                if (ok && ComputeCatalogStore.LastApiItemCount == 0)
                    await ComputeCatalogStore.RefreshFromApiAsync();

                OnLibraryChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ComputeCatalog] load failed: {ex}");
            }
        }

        private async Task LoadRateLibraryAsync()
        {
            try
            {
                // show cached first
                RateLibraryStore.ReloadFromDisk();
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] Cached disk items={RateLibraryStore.Items?.Count ?? 0}");

                // pull latest (requires logged-in token)
                var ok = await RateLibraryStore.RefreshFromApiAsync(SectionKey);

                System.Diagnostics.Debug.WriteLine(
                    $"[RateLibrary] Refresh ok={ok}, status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");

                // fallback if backend section naming differs
                if (ok && RateLibraryStore.LastApiItemCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[RateLibrary] Section returned 0. Retrying without sectionKey...");
                    await RateLibraryStore.RefreshFromApiAsync();

                    System.Diagnostics.Debug.WriteLine(
                        $"[RateLibrary] Retry(all) status={RateLibraryStore.LastApiStatusCode}, count={RateLibraryStore.LastApiItemCount}, msg={RateLibraryStore.LastApiMessage}");
                }

                OnLibraryChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] load failed: {ex}");
            }
        }

        /* -------------------- UI SAFE REBUILD -------------------- */

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

        /* -------------------- UI / FILTER / SORT -------------------- */

        private bool FilterRoofItem(object obj)
        {
            if (obj is RoofWorkItem item)
            {
                if (string.IsNullOrEmpty(SearchTerm))
                    return true;

                return (item.Description ?? "")
                    .IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        private void RecomputeAll()
        {
            RoofWorkItems.Clear();
            BuildRoofworkItem();
            RoofworkCollectionView?.Refresh();
        }

        public void RecomputeItemInPlace(int itemNo)
        {
            var existing = RoofWorkItems.FirstOrDefault(i => i.ItemNo == itemNo);
            if (existing == null) return;

            Func<RoofWorkItem>[] all =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16,
                ComputeItem17, ComputeItem18, ComputeItem19, ComputeItem20,
                ComputeItem21, ComputeItem22
            };

            RoofWorkItem? fresh = null;
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

            existing.RoofWorkBreakdownLines.Clear();
            foreach (var line in fresh.RoofWorkBreakdownLines)
                existing.RoofWorkBreakdownLines.Add(line);

            RoofworkCollectionView?.Refresh();
        }

        private void ShowDetails(object o)
        {
            if (o is RoofWorkItem item)
            {
                var detailedControl = new RoofworkDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () => { SelectedDetail = null; };
                SelectedDetail = detailedControl;
            }
        }

        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            RoofworkCollectionView.SortDescriptions.Clear();

            if (_isNetCostFilterOn)
                RoofworkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(RoofWorkItem.NetCost), ListSortDirection.Ascending));
        }

        private void CycleSort()
        {
            _currentSort = _currentSort switch
            {
                SortState.None => SortState.Overhead,
                SortState.Overhead => SortState.TotalCost,
                SortState.TotalCost => SortState.None,
                _ => SortState.None
            };

            RoofworkCollectionView.SortDescriptions.Clear();

            switch (_currentSort)
            {
                case SortState.Overhead:
                    RoofworkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(RoofWorkItem.OverheadValue), ListSortDirection.Ascending));
                    break;

                case SortState.TotalCost:
                    RoofworkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(RoofWorkItem.TotalCost), ListSortDirection.Ascending));
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

        /* -------------------- BUILD ITEMS -------------------- */

        private void BuildRoofworkItem()
        {
            Func<RoofWorkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16,
                ComputeItem17, ComputeItem18, ComputeItem19, ComputeItem20,
                ComputeItem21, ComputeItem22
            };

            foreach (var compute in computeMethods)
                RoofWorkItems.Add(compute());

            // ✅ Append dynamic compute definitions (ComputeCatalogStore)
            AppendApiComputeItems();

            // ✅ Append admin/DB rates (RateLibraryStore)  << THIS WAS MISSING
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
            int nextNo = RoofWorkItems.Count + 1;

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

                    var breakdown = new ObservableCollection<RoofWorkBreakdownLine>();

                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new RoofWorkBreakdownLine
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
                        breakdown.Add(new RoofWorkBreakdownLine
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
                        breakdown.Add(new RoofWorkBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new RoofWorkBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    RoofWorkItems.Add(new RoofWorkItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 0),
                        RoofWorkBreakdownLines = breakdown
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

        private void AppendAdminRateItems()
        {
            var rates = RateLibraryStore.Items;
            if (rates == null || rates.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] No rate items in store. File={RateLibraryStore.FilePath}");
                return;
            }

            int nextNo = RoofWorkItems.Count + 1;
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

                var breakdown = new ObservableCollection<RoofWorkBreakdownLine>();
                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        breakdown.Add(new RoofWorkBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice))
                        });
                    }
                }

                RoofWorkItems.Add(new RoofWorkItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 0),
                    RoofWorkBreakdownLines = breakdown
                });

                appended++;
            }

            System.Diagnostics.Debug.WriteLine($"[RateLibrary] Appended {appended} admin rate(s) for section '{SectionKey}'.");
        }

        /* -------------------- SHARED HELPERS -------------------- */

        private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
        {
            double ov = netCost * (OverheadPercent / 100);
            double pv = netCost * (ProfitPercent / 100);
            double total = netCost + ov + pv;
            return (ov, pv, total);
        }

        private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
        private double GetLabourRate(string name) => _helper.GetLabourRate(name);

        /* -------------------- COMPUTE METHODS --------------------
           ✅ Paste your existing ComputeItem1..ComputeItem16 here unchanged.
        */

        #region Compute Method

        private RoofWorkItem ComputeItem1()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("3 1/2x6' (1050mm x 1800mm x 4mm thick)");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 1, "Sheeting (975 x 1650)", 1.61);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Bolts", 4);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 1, "Add for waste and laps.", 30);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 1, "Add for waste on bolts/screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = boltRate * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate+ carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 1, "Total Gang Cost per m2", 0.3);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Sheeting (975 x 1650)", Quantity=sheetArea, Unit="m2",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Bolts", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on bolts/screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 1,
                Description= "Super lightweight (SLW) asbestos roofing sheet laid on purlins " +
                "(measured separately) with drive screws/nails to pitch " +
                "not exceeding 30o  (enclosed roof space) Note: Using sheet size 1050 x 1800mm.",
                Unit= "m2",
                NetCost= Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue= Math.Round(ohp.profitVal, 2),
                TotalCost= Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines= breakdown
            };

        }
        private RoofWorkItem ComputeItem2()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("3 1/2x8' (1050mm x 2400mm x 4mm thick)");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 2, "Sheeting (975 x 2250)", 2.19);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Bolts", 4);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 2, "Add for waste and laps.", 30);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 2, "Add for waste on bolts/screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = boltRate * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 2, "Total Gang Cost per m2", 0.3);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Sheeting (975 x 2250)", Quantity=sheetArea, Unit="m2",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Bolts", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on bolts/screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 2,
                Description = "As above but sheet size 1050 x 2400mm ditto. Net Covering Area: 975 x 2250mm",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem3()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("Two piece flat wing ridges, 1055mm long");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 3, "Ridge capping 1070mm long.", 0.905);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Drive screws", 4);

            //double wastePer = 30;
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 3, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate+ sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 3, "Total Gang Cost per m2", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Ridge capping 1070mm long.", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
				//new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
				//	TotalPrice=sheetWaste},
				new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 3,
                Description = "Two  piece flat wing asbestos ridge capping not exceeding 500mm girth. For SLW range. Overall Covering Length: 1055mm Net Covering Length: 905mm",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem4()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("Two piece corrugated wing ridges, 1055mm long");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 4, "Ridge capping 1055mm long.", 0.905);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Drive screws", 4);

            //double wastePer = 30;
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 4, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate + sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 4, "Total Gang Cost per m2", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Ridge capping 1055mm long.", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
				//new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
				//	TotalPrice=sheetWaste},
				new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 4,
                Description = "Two piece corrugated asbestos ridge capping not exceeding 500mm girth. For SLW range. Overall Covering Length: 1055mm Net Covering Length: 905mm",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem5()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("3 1/2x8' (1097mm x 2400mm x 4.5mm thick)");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 5, "Sheeting (1050 x 2250)", 2.36);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Bolts", 5);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add for waste and laps.", 30);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add for waste on bolts/screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm+ sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 5, "Total Gang Cost per m2", 0.3);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Sheeting (1050 x 2250)", Quantity=sheetArea, Unit="m2",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Bolts", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on bolts/screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 5,
                Description = "Super Seven asbestos roofing sheet laid on purlins (measured separately) with drive screws/nails to pitch not exceeding 30o  (enclosed roof space) Note: " +
                "Using sheet size 1097 x 2400mm.Net Covering Area: 1050 x 2250mm",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem6()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("3 1/2x6' (1097mm x 1800mm x 4.5mm thick)");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 6, "Sheeting (1050 x 1650)", 2.19);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Bolts", 5);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 6, "Add for waste and laps.", 30);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 6, "Add for waste on bolts/screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 6, "Total Gang Cost per m2", 0.3);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Sheeting (1050 x 1650)", Quantity=sheetArea, Unit="m2",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Bolts", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on bolts/screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 6,
                Description = "As above but sheet size 1097 x 1800mm ditto. Net Covering Area: 1050 x 1650mm",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem7()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("One piece flat wing ridges, 1650mm long");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 7, "Ridge capping 1070mm long.", 0.905);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Drive screws", 5);

            //double wastePer = 30;
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 7, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate + sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 7, "Total Gang Cost per m2", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Ridge capping 1070mm long.", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
				//new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
				//	TotalPrice=sheetWaste},
				new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 7,
                Description = "One  piece flat wing asbestos ridge capping not exceeding 500mm girth. For SLW range. Overall Covering Length: 1055mm Net Covering Length: 905mm ",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem8()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("Two piece corrugated wing ridges, 1187mm long");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 7, "Ridge capping 1055mm long.", 0.905);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Drive screws", 5);

            //double wastePer = 30;
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 7, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate + sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 7, "Total Gang Cost per m2", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Ridge capping 1055mm long.", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
				//new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
				//	TotalPrice=sheetWaste},
				new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 7,
                Description = "One  piece flat wing asbestos ridge capping not exceeding 500mm girth. For SLW range. Overall Covering Length: 1055mm Net Covering Length: 905mm ",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem9()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("0.70mm (22SWG) sheet, Stucco mill");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 9, "Material cost (900mm wide)", .9);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Bolts", 4);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 9, "Add for waste and laps.", 22);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 9, "Add for waste on bolts/screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 9, "Total Gang Cost per m", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Material cost (900mm wide)", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Bolts", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on bolts/screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m", Quantity=labourSqmPerHr, Unit="hr/m",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 9,
                Description = "Stucco Mill Corrugated Longspan aluminium roof sheeting (Alumaco) gauge 0.7mm as roof covering on pitch not exceeding 450, " +
                "held on to wooden purlins with drive screws. (enclosed roof space)",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem10()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("0.70mm (22SWG) sheet, Stucco mill");
            double boltCost = GetMaterialPrice("Hook Bolts (Roof Manufacturer's Specification)");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 10, "Material cost (900mm wide)", .9);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Hook bolts", 6);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 10, "Add for waste and laps.", 22);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 10, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 10, "Total Gang Cost per m", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Material cost (900mm wide)", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Hook bolts", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m", Quantity=labourSqmPerHr, Unit="hr/m",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 10,
                Description = "Stucco Mill Corrugated Longspan aluminium roof sheeting (Alumaco) gauge 0.7mm as roof covering on pitch not exceeding 450, held on" +
                " to steel or wooden purlins with hook bolts. (open roof space)",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem11()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("0.70mm (22SWG) sheet, coloured");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 11, "Material cost (900mm wide)", .9);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Drive screws", 4);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 11, "Add for waste and laps.", 22);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 11, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 11, "Total Gang Cost per m", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Material cost (900mm wide)", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m", Quantity=labourSqmPerHr, Unit="hr/m",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 11,
                Description = "Kolor Kote Corrugated Longspan aluminium roof sheeting (Alumaco) gauge 0.7mm as roof covering on pitch not exceeding 450, " +
                "held on to wooden purlins with drive screws. (enclosed roof space)",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem12()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("0.70mm (22SWG) sheet, coloured");
            double boltCost = GetMaterialPrice("Hook Bolts (Roof Manufacturer's Specification)");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 12, "Material cost (900mm wide)", .9);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Drive screws", 6);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 12, "Add for waste and laps.", 22);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 12, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 12, "Total Gang Cost per m", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Material cost (900mm wide)", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m", Quantity=labourSqmPerHr, Unit="hr/m",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 12,
                Description = "Kolor Kote Corrugated Longspan aluminium roof sheeting gauge 0.7mm as roof covering on pitch not exceeding 450, held on " +
                "to steel or wooden purlins with hook bolts. (open roof space)",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem13()
        {
            //MATERIAL COST
            double primerCost = GetMaterialPrice("Imper 66 primer")/200;
            double parallonCost = GetMaterialPrice("Water proof membrane - parallon NT4 (10 x 1m)")/10;

            double primerQty = UserRateEditStore.Current.Qty(SectionKey, 13, "Place primer to horizontal surface", 1);
            double parallonQty = UserRateEditStore.Current.Qty(SectionKey, 13, "Place and apply  parallon NT4 membrane", 1);

            double primerPer = UserRateEditStore.Current.Qty(SectionKey, 13, "Add waste.", 5);
            double parallonWastePer = UserRateEditStore.Current.Qty(SectionKey, 13, "Add waste.", 5);

            double primerRate = primerCost*primerQty;
            double primerWaste = primerRate * (primerPer / 100);
            double parallonRate = parallonCost * parallonQty;
            double parallonWaste = (parallonRate) * (parallonWastePer / 100);

            double totalMaterialCost = primerRate + primerWaste + parallonRate + parallonWaste;

            //PLANT COST
            double gasTorchCost = GetLabourRate("Gas torch and 50 or 70mm burner.");
            double gasCylinderCost = GetMaterialPrice("Gas (13 kg Cylinder)");

            double gasTorchQty = UserRateEditStore.Current.Qty(SectionKey, 13, "Gas Torch / Burner.", 1);
            double gasCylinderQty = UserRateEditStore.Current.Qty(SectionKey, 13, "Gas  (13 kg cylinder)", 7);

            double gasTouchRate = gasTorchCost*gasTorchQty;
            double gasCylinderRate = gasCylinderCost * gasCylinderQty;

            double totalPlantPerDay = gasTouchRate + gasCylinderRate;
            double totalPlantPerHr = totalPlantPerDay / 8;
            double plantOutputPerHr = UserRateEditStore.Current.Qty(SectionKey, 13, "Total Plant Cost per Output", 9);

            double plantCost = totalPlantPerHr / plantOutputPerHr;

            //LABOUR COST
            double labourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

            double labourOutput = UserRateEditStore.Current.Qty(SectionKey, 13, "Labour laying - skilled layer", .11);

            double labourRate = labourCost * labourOutput;

            double netCostPerSqm = totalMaterialCost + plantCost+ labourRate;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Place primer to horizontal surface", Quantity=primerQty, Unit="Litre/m2",
                    UnitPrice= primerCost, TotalPrice=primerRate},
                new RoofWorkBreakdownLine{ComponentName="Add waste.", Quantity=primerPer, Unit="%",
                    TotalPrice=primerWaste},
                new RoofWorkBreakdownLine{ComponentName="Place and apply  parallon NT4 membrane", Quantity=parallonQty,  Unit="m2", UnitPrice=parallonCost,
                    TotalPrice=parallonRate},
                new RoofWorkBreakdownLine{ComponentName="Add waste.", Quantity=parallonWastePer, Unit="%",
                    TotalPrice=parallonWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},

                new RoofWorkBreakdownLine{ComponentName="Gas Torch / Burner.", Quantity=gasTorchQty, Unit="per day", UnitPrice=gasTorchCost,
                    TotalPrice=gasTouchRate},
                new RoofWorkBreakdownLine{ComponentName="Gas  (13 kg cylinder)", Quantity=gasCylinderQty, Unit="kg/per day", UnitPrice=gasCylinderCost,
                    TotalPrice=gasCylinderRate},
                new RoofWorkBreakdownLine{ComponentName="Cost per day", TotalPrice=totalPlantPerDay},
                new RoofWorkBreakdownLine{ComponentName="Cost per hour", TotalPrice=totalPlantPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Plant Cost per Output", Quantity=plantOutputPerHr, Unit="m2/hr", UnitPrice=totalPlantPerHr,
                    TotalPrice=plantCost},

                new RoofWorkBreakdownLine{ComponentName="Labour laying - skilled layer", Quantity=labourOutput, Unit="hr/m2",
                    UnitPrice=labourCost, TotalPrice= labourRate},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new RoofWorkItem
            {
                ItemNo = 13,
                Description = "Single layer bituminous roof felt applied to level surface as waterproofing",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem14()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("Swan size 1.8 x 900mm")/20;
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 14, "Sheeting (975 x 2250)", 2.19);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 14, "Drive screws", 4);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 14, "Add for waste and laps.", 30);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 14, "Add for waste on bolts/screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            //double headmanQty = 1;
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 14, "Tradesman (Carpenter)", 2);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 14, "Labour", 2);

            //double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 14, "Total Gang Cost per m2", 0.3);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Sheeting (975 x 2250)", Quantity=sheetArea, Unit="m2",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on bolts/screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
				//new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
				//	TotalPrice=headmanRate},
				new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m2", Quantity=labourSqmPerHr, Unit="hr/m2",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 14,
                Description = "Corruagted zinc galvanised roofing sheet. Net Covering Area: 975 x 2250mm",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }
        private RoofWorkItem ComputeItem15()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("0.55mm (24SWG) sheet, Stucco mill");
            double boltCost = GetMaterialPrice("Drive Screws/Roofing Nails");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 15, "Material cost (900mm wide)", .9);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 15, "Drive screws", 4);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 15, "Add for waste and laps.", 22);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 15, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 15, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 15, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 15, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 15, "Total Gang Cost per m", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Material cost (900mm wide)", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m", Quantity=labourSqmPerHr, Unit="hr/m",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 15,
                Description = "Stucco Mill Corrugated Longspan aluminium roof sheeting (Alumaco) gauge 0.55mm as roof covering on pitch not exceeding 450, held on" +
                " to wooden purlins with drive screws. (enclosed roof space)",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        private RoofWorkItem ComputeItem16()
        {
            //MATERIAL COST
            double sheetCost = GetMaterialPrice("0.55mm (24SWG) sheet, Stucco mill");
            double boltCost = GetMaterialPrice("Hook Bolts (Roof Manufacturer's Specification)");

            double sheetArea = UserRateEditStore.Current.Qty(SectionKey, 16, "Material cost (900mm wide)", .9);
            double boltQty = UserRateEditStore.Current.Qty(SectionKey, 16, "Drive screws", 6);

            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 16, "Add for waste and laps.", 22);
            double boltWastePer = UserRateEditStore.Current.Qty(SectionKey, 16, "Add for waste on screws", 5);

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, 16, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, 16, "Tradesman (Carpenter)", 1);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, 16, "Labour", 1);

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = UserRateEditStore.Current.Qty(SectionKey, 16, "Total Gang Cost per m", 0.25);

            double labourPerSqm = labourPerHr * labourSqmPerHr;

            double netCostPerSqm = totalMaterialCost + labourPerSqm;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Material cost (900mm wide)", Quantity=sheetArea, Unit="m",
                    UnitPrice= sheetCost, TotalPrice=sheetCostPerSqm},
                new RoofWorkBreakdownLine{ComponentName="Add for waste and laps.", Quantity=wastePer, Unit="%",
                    TotalPrice=sheetWaste},
                new RoofWorkBreakdownLine{ComponentName="Drive screws", Quantity=boltQty,  Unit="No/m2", UnitPrice=boltCost,
                    TotalPrice=boltRate},
                new RoofWorkBreakdownLine{ComponentName="Add for waste on screws", Quantity=boltWastePer, Unit="%",
                    TotalPrice=boltWaste},
                new RoofWorkBreakdownLine{ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ComponentName="Headman", Quantity=headmanQty, Unit="N/hr", UnitPrice=headmanCost,
                    TotalPrice=headmanRate},
                new RoofWorkBreakdownLine{ComponentName="Tradesman (Carpenter)", Quantity=carpenterQty, Unit="N/hr",
                    UnitPrice=carpenterLabourCost, TotalPrice= carpenterLabourRate},
                new RoofWorkBreakdownLine{ComponentName="Labour", Quantity=labourQty, Unit="N/hr", UnitPrice=labourCost,
                    TotalPrice=labourRate},
                new RoofWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice= labourPerHr},
                new RoofWorkBreakdownLine{ComponentName="Total Gang Cost per m", Quantity=labourSqmPerHr, Unit="hr/m",
                UnitPrice=labourRate, TotalPrice=labourPerSqm},

                new RoofWorkBreakdownLine{ComponentName="Total Cost per m", Unit="m", TotalPrice=netCostPerSqm}


            };

            return new RoofWorkItem
            {
                ItemNo = 16,
                Description = "Stucco Mill Corrugated Longspan aluminium roof sheeting (Alumaco) gauge 0.55mm as roof covering on pitch not exceeding 450, held on " +
                "to steel or wooden purlins with hook bolts. (open roof space)",
                Unit = "m",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }


        /* ─────────────────────── ROOF CARPENTRY ───────────────────────
         *
         * Items 1-16 are roof COVERING. Several of their own descriptions say
         * "laid on purlins (measured separately)", and until now nothing
         * measured them: the product priced the sheeting and nothing holding it
         * up. Items 17-22 are the carpentry.
         *
         * Timber is hardwood, which is what a Nigerian roof is actually framed
         * in. The softwood rows exist for joinery and cost more.
         *
         * Quantities are per square metre of roof area ON SLOPE, except the
         * wall plate and fascia which are linear. Every one is editable, so a
         * QS working to different centres overrides the spacing rather than
         * rebuilding the rate.
         */

        /// <summary>Shared gang cost per hour: headman, carpenter and labourer, 1.4 gang factor.</summary>
        private double CarpentryGangPerHour(int itemNo, out double headmanRate, out double carpenterRate,
                                            out double labourRate, out double headmanCost,
                                            out double carpenterCost, out double labourCost)
        {
            headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            carpenterCost = (GetLabourRate("Carpenter") / 8) * 1.4;
            labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Headman", 1);
            double carpenterQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Tradesman (Carpenter)", 2);
            double labourQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Labour", 1);

            headmanRate = headmanCost * headmanQty;
            carpenterRate = carpenterCost * carpenterQty;
            labourRate = labourCost * labourQty;

            return headmanRate + carpenterRate + labourRate;
        }

        /// <summary>
        /// One timber component of a carpentry rate. Lengths per unit is derived from
        /// the spacing and the 3600mm stock length, so a QS changing centres gets the
        /// right number of lengths without doing the arithmetic.
        /// </summary>
        private static double LengthsPerUnit(double runPerUnitM, double stockLengthM = 3.6)
            => stockLengthM <= 0 ? 0 : runPerUnitM / stockLengthM;

        // 17 ── Wall plate, per metre
        private RoofWorkItem ComputeItem17()
        {
            const int no = 17;
            double plateCost = GetMaterialPrice("2x4\"x12' (50x100x3600mm)");
            double nailCost = GetMaterialPrice("Nails 4\"");
            double treatCost = GetMaterialPrice("Solignum (normal)");

            double plateQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 100mm hardwood wall plate", LengthsPerUnit(1.0));
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, no, "Add for waste and cutting.", 10);
            double nailQty = UserRateEditStore.Current.Qty(SectionKey, no, "Nails 4\"", 0.008);
            double treatQty = UserRateEditStore.Current.Qty(SectionKey, no, "Solignum anti-termite treatment", 0.02);

            double plateRate = plateCost * plateQty;
            double waste = plateRate * (wastePer / 100);
            double nailRate = nailCost * nailQty;
            double treatRate = treatCost * treatQty;
            double totalMaterialCost = plateRate + waste + nailRate + treatRate;

            double gang = CarpentryGangPerHour(no, out var hR, out var cR, out var lR, out var hC, out var cC, out var lC);
            double hrsPerM = UserRateEditStore.Current.Qty(SectionKey, no, "Total Gang Cost per m", 0.12);
            double labourPerM = gang * hrsPerM;

            double net = totalMaterialCost + labourPerM;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="50 x 100mm hardwood wall plate", Quantity=plateQty, Unit="Length/m", UnitPrice=plateCost, TotalPrice=plateRate},
                new RoofWorkBreakdownLine{ ComponentName="Add for waste and cutting.", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new RoofWorkBreakdownLine{ ComponentName="Nails 4\"", Quantity=nailQty, Unit="Bag/m", UnitPrice=nailCost, TotalPrice=nailRate},
                new RoofWorkBreakdownLine{ ComponentName="Solignum anti-termite treatment", Quantity=treatQty, Unit="Gal/m", UnitPrice=treatCost, TotalPrice=treatRate},
                new RoofWorkBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ ComponentName="Headman", Quantity=1, Unit="N/hr", UnitPrice=hC, TotalPrice=hR},
                new RoofWorkBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=2, Unit="N/hr", UnitPrice=cC, TotalPrice=cR},
                new RoofWorkBreakdownLine{ ComponentName="Labour", Quantity=1, Unit="N/hr", UnitPrice=lC, TotalPrice=lR},
                new RoofWorkBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice=gang},
                new RoofWorkBreakdownLine{ ComponentName="Total Gang Cost per m", Quantity=hrsPerM, Unit="hr/m", TotalPrice=labourPerM},
                new RoofWorkBreakdownLine{ ComponentName="Total", TotalPrice=net},
            };

            return new RoofWorkItem
            {
                ItemNo = no,
                Description = "50 x 100mm hardwood wall plate; bedded in cement mortar on blockwork, levelled, " +
                              "anchored and treated with anti-termite solution",
                Unit = "m",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        // 18 ── Rafters, per m2 of roof on slope
        private RoofWorkItem ComputeItem18()
        {
            const int no = 18;
            double rafterCost = GetMaterialPrice("2x4\"x12' (50x100x3600mm)");
            double nailCost = GetMaterialPrice("Nails 4\"");
            double treatCost = GetMaterialPrice("Solignum (normal)");

            // 600mm centres: 1 / 0.6 = 1.667 m of rafter in every m2.
            double spacing = UserRateEditStore.Current.Qty(SectionKey, no, "Rafter spacing (centres)", 0.6);
            double rafterQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 100mm hardwood rafters",
                                  LengthsPerUnit(spacing > 0 ? 1.0 / spacing : 0));
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, no, "Add for waste and cutting.", 12);
            double nailQty = UserRateEditStore.Current.Qty(SectionKey, no, "Nails 4\"", 0.015);
            double treatQty = UserRateEditStore.Current.Qty(SectionKey, no, "Solignum anti-termite treatment", 0.03);

            double rafterRate = rafterCost * rafterQty;
            double waste = rafterRate * (wastePer / 100);
            double nailRate = nailCost * nailQty;
            double treatRate = treatCost * treatQty;
            double totalMaterialCost = rafterRate + waste + nailRate + treatRate;

            double gang = CarpentryGangPerHour(no, out var hR, out var cR, out var lR, out var hC, out var cC, out var lC);
            double hrs = UserRateEditStore.Current.Qty(SectionKey, no, "Total Gang Cost per m2", 0.25);
            double labourPerSqm = gang * hrs;

            double net = totalMaterialCost + labourPerSqm;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Rafter spacing (centres)", Quantity=spacing, Unit="m"},
                new RoofWorkBreakdownLine{ ComponentName="50 x 100mm hardwood rafters", Quantity=rafterQty, Unit="Length/m2", UnitPrice=rafterCost, TotalPrice=rafterRate},
                new RoofWorkBreakdownLine{ ComponentName="Add for waste and cutting.", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new RoofWorkBreakdownLine{ ComponentName="Nails 4\"", Quantity=nailQty, Unit="Bag/m2", UnitPrice=nailCost, TotalPrice=nailRate},
                new RoofWorkBreakdownLine{ ComponentName="Solignum anti-termite treatment", Quantity=treatQty, Unit="Gal/m2", UnitPrice=treatCost, TotalPrice=treatRate},
                new RoofWorkBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ ComponentName="Headman", Quantity=1, Unit="N/hr", UnitPrice=hC, TotalPrice=hR},
                new RoofWorkBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=2, Unit="N/hr", UnitPrice=cC, TotalPrice=cR},
                new RoofWorkBreakdownLine{ ComponentName="Labour", Quantity=1, Unit="N/hr", UnitPrice=lC, TotalPrice=lR},
                new RoofWorkBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice=gang},
                new RoofWorkBreakdownLine{ ComponentName="Total Gang Cost per m2", Quantity=hrs, Unit="hr/m2", TotalPrice=labourPerSqm},
                new RoofWorkBreakdownLine{ ComponentName="Total", TotalPrice=net},
            };

            return new RoofWorkItem
            {
                ItemNo = no,
                Description = "50 x 100mm hardwood rafters at 600mm centres; cut, fitted, spiked to wall plate and " +
                              "ridge, and treated with anti-termite solution",
                Unit = "m2",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        // 19 ── Purlins, per m2 of roof on slope
        private RoofWorkItem ComputeItem19()
        {
            const int no = 19;
            double purlinCost = GetMaterialPrice("2x3\"x12' (50x75x3600mm) - Hardwood");
            double nailCost = GetMaterialPrice("Nails 3\"");
            double treatCost = GetMaterialPrice("Solignum (normal)");

            double spacing = UserRateEditStore.Current.Qty(SectionKey, no, "Purlin spacing (centres)", 0.9);
            double purlinQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 75mm hardwood purlins",
                                  LengthsPerUnit(spacing > 0 ? 1.0 / spacing : 0));
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, no, "Add for waste and cutting.", 10);
            double nailQty = UserRateEditStore.Current.Qty(SectionKey, no, "Nails 3\"", 0.012);
            double treatQty = UserRateEditStore.Current.Qty(SectionKey, no, "Solignum anti-termite treatment", 0.02);

            double purlinRate = purlinCost * purlinQty;
            double waste = purlinRate * (wastePer / 100);
            double nailRate = nailCost * nailQty;
            double treatRate = treatCost * treatQty;
            double totalMaterialCost = purlinRate + waste + nailRate + treatRate;

            double gang = CarpentryGangPerHour(no, out var hR, out var cR, out var lR, out var hC, out var cC, out var lC);
            double hrs = UserRateEditStore.Current.Qty(SectionKey, no, "Total Gang Cost per m2", 0.15);
            double labourPerSqm = gang * hrs;

            double net = totalMaterialCost + labourPerSqm;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Purlin spacing (centres)", Quantity=spacing, Unit="m"},
                new RoofWorkBreakdownLine{ ComponentName="50 x 75mm hardwood purlins", Quantity=purlinQty, Unit="Length/m2", UnitPrice=purlinCost, TotalPrice=purlinRate},
                new RoofWorkBreakdownLine{ ComponentName="Add for waste and cutting.", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new RoofWorkBreakdownLine{ ComponentName="Nails 3\"", Quantity=nailQty, Unit="Bag/m2", UnitPrice=nailCost, TotalPrice=nailRate},
                new RoofWorkBreakdownLine{ ComponentName="Solignum anti-termite treatment", Quantity=treatQty, Unit="Gal/m2", UnitPrice=treatCost, TotalPrice=treatRate},
                new RoofWorkBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ ComponentName="Headman", Quantity=1, Unit="N/hr", UnitPrice=hC, TotalPrice=hR},
                new RoofWorkBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=2, Unit="N/hr", UnitPrice=cC, TotalPrice=cR},
                new RoofWorkBreakdownLine{ ComponentName="Labour", Quantity=1, Unit="N/hr", UnitPrice=lC, TotalPrice=lR},
                new RoofWorkBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice=gang},
                new RoofWorkBreakdownLine{ ComponentName="Total Gang Cost per m2", Quantity=hrs, Unit="hr/m2", TotalPrice=labourPerSqm},
                new RoofWorkBreakdownLine{ ComponentName="Total", TotalPrice=net},
            };

            return new RoofWorkItem
            {
                ItemNo = no,
                Description = "50 x 75mm hardwood purlins at 900mm centres; cut, fitted and spiked to rafters, " +
                              "treated with anti-termite solution",
                Unit = "m2",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        // 20 ── Ceiling noggins / battens, per m2
        private RoofWorkItem ComputeItem20()
        {
            const int no = 20;
            double noggingCost = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
            double nailCost = GetMaterialPrice("Nails 3\"");
            double treatCost = GetMaterialPrice("Solignum (normal)");

            double spacing = UserRateEditStore.Current.Qty(SectionKey, no, "Nogging spacing (centres)", 0.6);
            double noggingQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 50mm hardwood ceiling noggings",
                                   LengthsPerUnit(spacing > 0 ? 1.0 / spacing : 0));
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, no, "Add for waste and cutting.", 10);
            double nailQty = UserRateEditStore.Current.Qty(SectionKey, no, "Nails 3\"", 0.010);
            double treatQty = UserRateEditStore.Current.Qty(SectionKey, no, "Solignum anti-termite treatment", 0.02);

            double noggingRate = noggingCost * noggingQty;
            double waste = noggingRate * (wastePer / 100);
            double nailRate = nailCost * nailQty;
            double treatRate = treatCost * treatQty;
            double totalMaterialCost = noggingRate + waste + nailRate + treatRate;

            double gang = CarpentryGangPerHour(no, out var hR, out var cR, out var lR, out var hC, out var cC, out var lC);
            double hrs = UserRateEditStore.Current.Qty(SectionKey, no, "Total Gang Cost per m2", 0.12);
            double labourPerSqm = gang * hrs;

            double net = totalMaterialCost + labourPerSqm;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="Nogging spacing (centres)", Quantity=spacing, Unit="m"},
                new RoofWorkBreakdownLine{ ComponentName="50 x 50mm hardwood ceiling noggings", Quantity=noggingQty, Unit="Length/m2", UnitPrice=noggingCost, TotalPrice=noggingRate},
                new RoofWorkBreakdownLine{ ComponentName="Add for waste and cutting.", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new RoofWorkBreakdownLine{ ComponentName="Nails 3\"", Quantity=nailQty, Unit="Bag/m2", UnitPrice=nailCost, TotalPrice=nailRate},
                new RoofWorkBreakdownLine{ ComponentName="Solignum anti-termite treatment", Quantity=treatQty, Unit="Gal/m2", UnitPrice=treatCost, TotalPrice=treatRate},
                new RoofWorkBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ ComponentName="Headman", Quantity=1, Unit="N/hr", UnitPrice=hC, TotalPrice=hR},
                new RoofWorkBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=2, Unit="N/hr", UnitPrice=cC, TotalPrice=cR},
                new RoofWorkBreakdownLine{ ComponentName="Labour", Quantity=1, Unit="N/hr", UnitPrice=lC, TotalPrice=lR},
                new RoofWorkBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice=gang},
                new RoofWorkBreakdownLine{ ComponentName="Total Gang Cost per m2", Quantity=hrs, Unit="hr/m2", TotalPrice=labourPerSqm},
                new RoofWorkBreakdownLine{ ComponentName="Total", TotalPrice=net},
            };

            return new RoofWorkItem
            {
                ItemNo = no,
                Description = "50 x 50mm hardwood ceiling noggings at 600mm centres; cut, fitted and nailed to " +
                              "rafters or ceiling joists to receive ceiling finish",
                Unit = "m2",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        // 21 ── Fascia board, per metre
        private RoofWorkItem ComputeItem21()
        {
            const int no = 21;
            double fasciaCost = GetMaterialPrice("1x12\"x12' (25x300x4200mm)");
            double nailCost = GetMaterialPrice("Nails 3\"");
            double treatCost = GetMaterialPrice("Solignum (normal)");

            double fasciaQty = UserRateEditStore.Current.Qty(SectionKey, no, "25 x 300mm hardwood fascia board", LengthsPerUnit(1.0, 4.2));
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, no, "Add for waste and cutting.", 12);
            double nailQty = UserRateEditStore.Current.Qty(SectionKey, no, "Nails 3\"", 0.006);
            double treatQty = UserRateEditStore.Current.Qty(SectionKey, no, "Solignum anti-termite treatment", 0.02);

            double fasciaRate = fasciaCost * fasciaQty;
            double waste = fasciaRate * (wastePer / 100);
            double nailRate = nailCost * nailQty;
            double treatRate = treatCost * treatQty;
            double totalMaterialCost = fasciaRate + waste + nailRate + treatRate;

            double gang = CarpentryGangPerHour(no, out var hR, out var cR, out var lR, out var hC, out var cC, out var lC);
            double hrsPerM = UserRateEditStore.Current.Qty(SectionKey, no, "Total Gang Cost per m", 0.15);
            double labourPerM = gang * hrsPerM;

            double net = totalMaterialCost + labourPerM;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="25 x 300mm hardwood fascia board", Quantity=fasciaQty, Unit="Length/m", UnitPrice=fasciaCost, TotalPrice=fasciaRate},
                new RoofWorkBreakdownLine{ ComponentName="Add for waste and cutting.", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new RoofWorkBreakdownLine{ ComponentName="Nails 3\"", Quantity=nailQty, Unit="Bag/m", UnitPrice=nailCost, TotalPrice=nailRate},
                new RoofWorkBreakdownLine{ ComponentName="Solignum anti-termite treatment", Quantity=treatQty, Unit="Gal/m", UnitPrice=treatCost, TotalPrice=treatRate},
                new RoofWorkBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ ComponentName="Headman", Quantity=1, Unit="N/hr", UnitPrice=hC, TotalPrice=hR},
                new RoofWorkBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=2, Unit="N/hr", UnitPrice=cC, TotalPrice=cR},
                new RoofWorkBreakdownLine{ ComponentName="Labour", Quantity=1, Unit="N/hr", UnitPrice=lC, TotalPrice=lR},
                new RoofWorkBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice=gang},
                new RoofWorkBreakdownLine{ ComponentName="Total Gang Cost per m", Quantity=hrsPerM, Unit="hr/m", TotalPrice=labourPerM},
                new RoofWorkBreakdownLine{ ComponentName="Total", TotalPrice=net},
            };

            return new RoofWorkItem
            {
                ItemNo = no,
                Description = "25 x 300mm hardwood fascia board; wrought, cut to length, fixed to rafter feet and " +
                              "treated, including mitres at angles",
                Unit = "m",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        // 22 ── Complete roof carpentry, per m2 of roof on slope
        private RoofWorkItem ComputeItem22()
        {
            const int no = 22;
            double rafterCost = GetMaterialPrice("2x4\"x12' (50x100x3600mm)");
            double purlinCost = GetMaterialPrice("2x3\"x12' (50x75x3600mm) - Hardwood");
            double ridgeCost = GetMaterialPrice("2x6\"x12' (50x150x3600mm)");
            double nailCost = GetMaterialPrice("Nails 4\"");
            double treatCost = GetMaterialPrice("Solignum (normal)");

            double rafterQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 100mm rafters at 600mm centres", LengthsPerUnit(1.0 / 0.6));
            double purlinQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 75mm purlins at 900mm centres", LengthsPerUnit(1.0 / 0.9));
            double plateQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 100mm wall plate", LengthsPerUnit(0.15));
            double strutQty = UserRateEditStore.Current.Qty(SectionKey, no, "50 x 150mm ridge board, struts and bracing", LengthsPerUnit(0.25));
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, no, "Add for waste and cutting.", 12);
            double nailQty = UserRateEditStore.Current.Qty(SectionKey, no, "Nails 4\"", 0.035);
            double treatQty = UserRateEditStore.Current.Qty(SectionKey, no, "Solignum anti-termite treatment", 0.06);

            double rafterRate = rafterCost * rafterQty;
            double purlinRate = purlinCost * purlinQty;
            double plateRate = rafterCost * plateQty;
            double strutRate = ridgeCost * strutQty;
            double timberSub = rafterRate + purlinRate + plateRate + strutRate;
            double waste = timberSub * (wastePer / 100);
            double nailRate = nailCost * nailQty;
            double treatRate = treatCost * treatQty;
            double totalMaterialCost = timberSub + waste + nailRate + treatRate;

            double gang = CarpentryGangPerHour(no, out var hR, out var cR, out var lR, out var hC, out var cC, out var lC);
            double hrs = UserRateEditStore.Current.Qty(SectionKey, no, "Total Gang Cost per m2", 0.55);
            double labourPerSqm = gang * hrs;

            double net = totalMaterialCost + labourPerSqm;
            var ohp = ApplyOHP(net);

            var breakdown = new ObservableCollection<RoofWorkBreakdownLine>
            {
                new RoofWorkBreakdownLine{ ComponentName="50 x 100mm rafters at 600mm centres", Quantity=rafterQty, Unit="Length/m2", UnitPrice=rafterCost, TotalPrice=rafterRate},
                new RoofWorkBreakdownLine{ ComponentName="50 x 75mm purlins at 900mm centres", Quantity=purlinQty, Unit="Length/m2", UnitPrice=purlinCost, TotalPrice=purlinRate},
                new RoofWorkBreakdownLine{ ComponentName="50 x 100mm wall plate", Quantity=plateQty, Unit="Length/m2", UnitPrice=rafterCost, TotalPrice=plateRate},
                new RoofWorkBreakdownLine{ ComponentName="50 x 150mm ridge board, struts and bracing", Quantity=strutQty, Unit="Length/m2", UnitPrice=ridgeCost, TotalPrice=strutRate},
                new RoofWorkBreakdownLine{ ComponentName="Sub-total: timber", TotalPrice=timberSub},
                new RoofWorkBreakdownLine{ ComponentName="Add for waste and cutting.", Quantity=wastePer, Unit="%", TotalPrice=waste},
                new RoofWorkBreakdownLine{ ComponentName="Nails 4\"", Quantity=nailQty, Unit="Bag/m2", UnitPrice=nailCost, TotalPrice=nailRate},
                new RoofWorkBreakdownLine{ ComponentName="Solignum anti-termite treatment", Quantity=treatQty, Unit="Gal/m2", UnitPrice=treatCost, TotalPrice=treatRate},
                new RoofWorkBreakdownLine{ ComponentName="Total Material", TotalPrice=totalMaterialCost},
                new RoofWorkBreakdownLine{ ComponentName="Headman", Quantity=1, Unit="N/hr", UnitPrice=hC, TotalPrice=hR},
                new RoofWorkBreakdownLine{ ComponentName="Tradesman (Carpenter)", Quantity=2, Unit="N/hr", UnitPrice=cC, TotalPrice=cR},
                new RoofWorkBreakdownLine{ ComponentName="Labour", Quantity=1, Unit="N/hr", UnitPrice=lC, TotalPrice=lR},
                new RoofWorkBreakdownLine{ ComponentName="Gang Cost per hour", TotalPrice=gang},
                new RoofWorkBreakdownLine{ ComponentName="Total Gang Cost per m2", Quantity=hrs, Unit="hr/m2", TotalPrice=labourPerSqm},
                new RoofWorkBreakdownLine{ ComponentName="Total", TotalPrice=net},
            };

            return new RoofWorkItem
            {
                ItemNo = no,
                Description = "Complete hardwood roof carpentry; 50 x 100mm rafters at 600mm centres, 50 x 75mm " +
                              "purlins at 900mm centres, wall plate, ridge board, struts and bracing, cut, fitted, " +
                              "spiked and treated with anti-termite solution",
                Unit = "m2",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                RoofWorkBreakdownLines = breakdown
            };
        }

        #endregion
    }
}
