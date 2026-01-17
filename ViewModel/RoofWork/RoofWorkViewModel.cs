using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16
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

            var defs = ComputeCatalogStore.Items;
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

            double sheetArea = 1.61;
            double boltQty = 4;

            double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = boltRate * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate+ carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.3;

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

            double sheetArea = 2.19;
            double boltQty = 4;

            double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = boltRate * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.3;

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

            double sheetArea = 0.905;
            double boltQty = 4;

            //double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate+ sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = 0.905;
            double boltQty = 4;

            //double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate + sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = 2.36;
            double boltQty = 5;

            double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm+ sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.3;

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

            double sheetArea = 2.19;
            double boltQty = 5;

            double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.3;

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

            double sheetArea = 0.905;
            double boltQty = 5;

            //double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate + sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = 0.905;
            double boltQty = 5;

            //double wastePer = 30;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            //double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate + sheetCostPerSqm) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = .9;
            double boltQty = 4;

            double wastePer = 22;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = .9;
            double boltQty = 6;

            double wastePer = 22;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = .9;
            double boltQty = 4;

            double wastePer = 22;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = .9;
            double boltQty = 6;

            double wastePer = 22;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double primerQty = 1;
            double parallonQty = 1;

            double primerPer = 5;
            double parallonWastePer = 5;

            double primerRate = primerCost*primerQty;
            double primerWaste = primerRate * (primerPer / 100);
            double parallonRate = parallonCost * parallonQty;
            double parallonWaste = (parallonRate) * (parallonWastePer / 100);

            double totalMaterialCost = primerRate + primerWaste + parallonRate + parallonWaste;

            //PLANT COST
            double gasTorchCost = GetLabourRate("Gas torch and 50 or 70mm burner.");
            double gasCylinderCost = GetMaterialPrice("Gas (13 kg Cylinder)");

            double gasTorchQty = 1;
            double gasCylinderQty = 7;

            double gasTouchRate = gasTorchCost*gasTorchQty;
            double gasCylinderRate = gasCylinderCost * gasCylinderQty;

            double totalPlantPerDay = gasTouchRate + gasCylinderRate;
            double totalPlantPerHr = totalPlantPerDay / 8;
            double plantOutputPerHr = 9;

            double plantCost = totalPlantPerHr / plantOutputPerHr;

            //LABOUR COST
            double labourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;

            double labourOutput = .11;

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

            double sheetArea = 2.19;
            double boltQty = 4;

            double wastePer = 30;
            double boltWastePer = 5;

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
            double carpenterQty = 2;
            double labourQty = 2;

            //double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.3;

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

            double sheetArea = .9;
            double boltQty = 4;

            double wastePer = 22;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

            double sheetArea = .9;
            double boltQty = 6;

            double wastePer = 22;
            double boltWastePer = 5;

            double sheetCostPerSqm = sheetCost / sheetArea;
            double sheetWaste = sheetCostPerSqm * (wastePer / 100);
            double boltRate = boltCost * boltQty;
            double boltWaste = (boltRate) * (boltWastePer / 100);

            double totalMaterialCost = sheetCostPerSqm + sheetWaste + boltRate + boltWaste;

            //LABOUR COST
            double headmanCost = (GetLabourRate("Headman") / 8) * 1.4;
            double carpenterLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double headmanQty = 1;
            double carpenterQty = 1;
            double labourQty = 1;

            double headmanRate = headmanCost * headmanQty;
            double carpenterLabourRate = carpenterLabourCost * carpenterQty;
            double labourRate = labourCost * labourQty;

            double labourPerHr = headmanRate + carpenterLabourRate + labourRate;
            double labourSqmPerHr = 0.25;

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

        #endregion
    }
}
