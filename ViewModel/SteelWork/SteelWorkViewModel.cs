using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.Painting;
using ADLMRateGen.ViewModel.RoofWork;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.SteelWork
{
    public class SteelWorkViewModel : ViewModelBase
    {
        private readonly GetItemsFromDB _helper;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        // ─── Sorting / filtering helpers ──────────────────────────────────────────────
        private bool _isNetCostFilterOn = false;
        private SortState _currentSort = SortState.None;

        private const string SectionKey = SectionKeys.Steelwork; // ✅ Steel section key
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
                    SteelWorkCollectionView?.Refresh();
                }
            }
        }

        public ICommand RecomputeCommand { get; }
        public ICommand ShowDetailsCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand AddCustomRateCommand { get; }

        public SteelWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);

            // Lib changes
            matLib.LibraryChanged += OnLibraryChanged;
            labourLib.LibraryChanged += OnLibraryChanged;

            // Views
            SteelWorkCollectionView = CollectionViewSource.GetDefaultView(SteelWorkItems);
            SteelWorkCollectionView.Filter = FilterSteelworkItem;

            // Commands
            RecomputeCommand = new DelegateCommand(_ => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
            FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
            SortCommand = new DelegateCommand(_ => CycleSort());
            AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

            // Compute engine
            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);

            // ✅ Disk-first (offline)
            ComputeCatalogStore.ReloadFromDisk();
            RateLibraryStore.ReloadFromDisk();

            // ✅ Listen to stores like GroundWork
            ComputeCatalogStore.Changed += OnLibraryChanged;
            RateLibraryStore.Changed += OnLibraryChanged;

            // ✅ Start API refresh (async)
            _ = LoadComputeCatalogForSectionAsync();
            _ = LoadRateLibraryAsync();

            // ✅ Build initial items immediately from cache
            RecomputeAll();

            // Currency updates
            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Rate) or nameof(CurrencyService.Code))
                    RecomputeAll();
            };

            UserRateEditStore.Current.OverridesChanged += (_, __) =>
            {
                void Refresh()
                {
                    var nos = SteelWorkItems.Select(i => i.ItemNo).ToList();
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

                // fallback if section mismatch / empty
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
                // Show cached first
                RateLibraryStore.ReloadFromDisk();
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] Cached disk items={RateLibraryStore.Items?.Count ?? 0}");

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

        private bool FilterSteelworkItem(object obj)
        {
            if (obj is SteelworkItem item)
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
            SteelWorkItems.Clear();
            BuildSteelworkItem();
            SteelWorkCollectionView?.Refresh();
        }

        public void RecomputeItemInPlace(int itemNo)
        {
            var existing = SteelWorkItems.FirstOrDefault(i => i.ItemNo == itemNo);
            if (existing == null) return;

            Func<SteelworkItem>[] all =
            {
                ComputeItem1, ComputeItem2, ComputeItem3,
                ComputeItem4, ComputeItem5, ComputeItem6, ComputeItem7,
                ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11,
                ComputeItem12
            };

            SteelworkItem? fresh = null;
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

            existing.SteelWorkBreakdownLines.Clear();
            foreach (var line in fresh.SteelWorkBreakdownLines)
                existing.SteelWorkBreakdownLines.Add(line);

            SteelWorkCollectionView?.Refresh();
        }

        private void ShowDetails(object o)
        {
            if (o is SteelworkItem item)
            {
                var detailedControl = new SteelWorkDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () => { SelectedDetail = null; };
                SelectedDetail = detailedControl;
            }
        }

        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            SteelWorkCollectionView.SortDescriptions.Clear();

            if (_isNetCostFilterOn)
                SteelWorkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(SteelworkItem.NetCost), ListSortDirection.Ascending));
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

            SteelWorkCollectionView.SortDescriptions.Clear();

            switch (_currentSort)
            {
                case SortState.Overhead:
                    SteelWorkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(SteelworkItem.OverheadValue), ListSortDirection.Ascending));
                    break;

                case SortState.TotalCost:
                    SteelWorkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(SteelworkItem.TotalCost), ListSortDirection.Ascending));
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

        private void BuildSteelworkItem()
        {
            Func<SteelworkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3,
                ComputeItem4, ComputeItem5, ComputeItem6, ComputeItem7,
                ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11,
                ComputeItem12
            };

            foreach (var compute in computeMethods)
                SteelWorkItems.Add(compute());

            // ✅ API compute definitions
            AppendApiComputeItems();

            // ✅ Admin/DB rates (RateLibraryStore) like GroundWork
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
            int nextNo = SteelWorkItems.Count + 1;

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

                    var breakdown = new ObservableCollection<SteelWorkBreakdownLine>();

                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new SteelWorkBreakdownLine
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
                        breakdown.Add(new SteelWorkBreakdownLine
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
                        breakdown.Add(new SteelWorkBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new SteelWorkBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    SteelWorkItems.Add(new SteelworkItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 2),
                        SteelWorkBreakdownLines = breakdown
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

            int nextNo = SteelWorkItems.Count + 1;
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

                var breakdown = new ObservableCollection<SteelWorkBreakdownLine>();
                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        breakdown.Add(new SteelWorkBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice))
                        });
                    }
                }

                SteelWorkItems.Add(new SteelworkItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 2),
                    SteelWorkBreakdownLines = breakdown
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

        public double GetNetValue(Func<PaintWorkItem> computeItemFunc)
        {
            var item = computeItemFunc();
            return item.NetCost;
        }

        public double GetSteelNetValue(Func<SteelworkItem> computeFunc)
        {
            return computeFunc().NetCost;
        }

        /* -------------------- YOUR EXISTING COMPUTE ITEMS -------------------- */

        public SteelworkItem ComputeItem1()
        {
            double brushCost = GetLabourRate("Power Brush");
            double brushQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Power Brush", 1);
            double brushTotal = brushCost * brushQty;

            double brushPer = UserRateEditStore.Current.Qty(SectionKey, 1, "Allow for Brushes", 10);
            double brushOv = brushTotal * (brushPer / 100);

            double artisanCost = GetLabourRate("Semi skilled") * 1.4;
            double artisanQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Skilled/Artisan", 1);
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
                Unit = "m",
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

            double compressorQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Compressor", 0.025);
            double fuelQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Fuel (Diesel)", 45);
            double oilPer = UserRateEditStore.Current.Qty(SectionKey, 2, "Oil and consumables (per day)", 3);
            double sandPotQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Sand Pot", 0.025);
            double respiratoryQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Respiratory gear.", 0.025);
            double gritQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Grit", 0.15);

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;
            double blastingForemanCost = GetLabourRate("Foreman") * 1.4;

            double blastingOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Blasting operator.", 1);
            double blastingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Labour (for loading sand pot)", 3);
            double blastingForemanQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Foreman", 1);

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
            double blastingForemanRate = blastingForemanCost * blastingForemanQty;

            double blastingLabour = blastingOperatorRate + blastingLabourRate + blastingForemanRate;
            double blastingOutputDaily = UserRateEditStore.Current.Qty(SectionKey, 2, "Labour Output", 300);

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

            double compressorQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Compressor", 0.025);
            double fuelQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Fuel (Diesel)", 45);
            double oilPer = UserRateEditStore.Current.Qty(SectionKey, 3, "Oil and consumables (per day)", 3);
            double sandPotQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Sand Pot", 0.025);
            double respiratoryQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Respiratory gear.", 0.025);
            double gritQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Grit", 0.15);

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;
            double blastingForemanCost = GetLabourRate("Foreman") * 1.4;

            double blastingOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Blasting operator.", 1);
            double blastingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Labour (for loading sand pot)", 3);
            double blastingForemanQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Foreman", 1);

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
            double blastingForemanRate = blastingForemanCost * blastingForemanQty;

            double blastingLabour = blastingOperatorRate + blastingLabourRate + blastingForemanRate;
            double blastingOutputDaily = UserRateEditStore.Current.Qty(SectionKey, 3, "Labour Output", 300);

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

        /* ─────────────────── STRUCTURAL STEELWORK ───────────────────
         *
         * Items 1-3 are surface preparation. Until now that was the whole
         * section: the product could clean steel but could not price any.
         *
         * Items 4-11 are the structural work. Each is supply, fabricate and
         * erect, because that is how a Nigerian bill measures it, and each is
         * offered per tonne AND per kg, because steelwork is taken off both ways:
         * tonnage from a steel schedule, kilos from a detailed takeoff.
         *
         * The per-kg items divide the per-tonne net by 1000 rather than running
         * their own build-up, so the two can never drift apart. Change a
         * quantity on the tonne item and the kg item follows.
         *
         * Protective treatment is deliberately NOT included. Items 1-3 price it
         * separately, which is the convention, and folding it in here would
         * double count for anyone who bills both.
         */

        /// <summary>
        /// Shared structural steel build-up, per tonne. Every section type uses the
        /// same shape and differs only in the material row it draws from, since
        /// fabrication and erection effort per tonne is broadly the same whether the
        /// steel arrives as a beam, a column or a channel.
        /// </summary>
        private (double net, ObservableCollection<SteelWorkBreakdownLine> lines) StructuralSteelPerTonne(
            int itemNo, string materialName, string materialLabel,
            double defaultGangHours, double defaultCraneHours)
        {
            double sectionCost = GetMaterialPrice(materialName);
            double plateCost = GetMaterialPrice("Steel plate, supply");

            double sectionQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, materialLabel, 1);
            double offcutPer = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Add for offcuts and fabrication waste.", 5);
            double cleatPer = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Connection plates, cleats and base plates", 4);

            double sectionTotal = sectionCost * sectionQty;
            double offcut = sectionTotal * (offcutPer / 100);
            double cleats = plateCost * (cleatPer / 100);
            double materialTotal = sectionTotal + offcut + cleats;

            // Labour, expressed the way every other engine does it: day rate over 8
            // hours with the 1.4 gang factor.
            double welderCost = (GetLabourRate("Welder") / 8) * 1.4;
            double fixerCost = (GetLabourRate("Steelfixer") / 8) * 1.4;
            double labourerCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double foremanCost = (GetLabourRate("Foreman") / 8) * 1.4;

            double gangHours = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Fabrication and erection gang", defaultGangHours);
            double welderQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Welder", 1);
            double fixerQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Steelfixer", 1);
            double labourerQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Labourer", 2);
            double foremanQty = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Foreman", 0.5);

            double gangPerHour = welderCost * welderQty + fixerCost * fixerQty
                               + labourerCost * labourerQty + foremanCost * foremanQty;
            double labourTotal = gangPerHour * gangHours;

            // Plant. The crane is the erection cost and is the reason a tonne of
            // steel in the air costs more than a tonne on the ground.
            double weldPlantCost = GetLabourRate("Welding machine (big)") / 8;
            double torchCost = GetLabourRate("Gas torch and 50 or 70mm burner.") / 8;
            double craneCost = GetLabourRate("Mobile crane-30 ton") / 8;

            double craneHours = UserRateEditStore.Current.Qty(SectionKey, itemNo, "Mobile crane, erection", defaultCraneHours);
            double weldPlantTotal = weldPlantCost * gangHours;
            double torchTotal = torchCost * gangHours;
            double craneTotal = craneCost * craneHours;
            double plantTotal = weldPlantTotal + torchTotal + craneTotal;

            double net = materialTotal + labourTotal + plantTotal;

            var lines = new ObservableCollection<SteelWorkBreakdownLine>
            {
                new SteelWorkBreakdownLine{ComponentName=materialLabel, Quantity=sectionQty, Unit="Tonne", UnitPrice=sectionCost, TotalPrice=sectionTotal},
                new SteelWorkBreakdownLine{ComponentName="Add for offcuts and fabrication waste.", Quantity=offcutPer, Unit="%", TotalPrice=offcut},
                new SteelWorkBreakdownLine{ComponentName="Connection plates, cleats and base plates", Quantity=cleatPer, Unit="%", UnitPrice=plateCost, TotalPrice=cleats},
                new SteelWorkBreakdownLine{ComponentName="Total Material", TotalPrice=materialTotal},

                new SteelWorkBreakdownLine{ComponentName="Welder", Quantity=welderQty, Unit="N/hr", UnitPrice=welderCost, TotalPrice=welderCost*welderQty},
                new SteelWorkBreakdownLine{ComponentName="Steelfixer", Quantity=fixerQty, Unit="N/hr", UnitPrice=fixerCost, TotalPrice=fixerCost*fixerQty},
                new SteelWorkBreakdownLine{ComponentName="Labourer", Quantity=labourerQty, Unit="N/hr", UnitPrice=labourerCost, TotalPrice=labourerCost*labourerQty},
                new SteelWorkBreakdownLine{ComponentName="Foreman", Quantity=foremanQty, Unit="N/hr", UnitPrice=foremanCost, TotalPrice=foremanCost*foremanQty},
                new SteelWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice=gangPerHour},
                new SteelWorkBreakdownLine{ComponentName="Fabrication and erection gang", Quantity=gangHours, Unit="hr/Tonne", TotalPrice=labourTotal},

                new SteelWorkBreakdownLine{ComponentName="Welding machine (big)", Quantity=gangHours, Unit="hr", UnitPrice=weldPlantCost, TotalPrice=weldPlantTotal},
                new SteelWorkBreakdownLine{ComponentName="Gas torch and burner", Quantity=gangHours, Unit="hr", UnitPrice=torchCost, TotalPrice=torchTotal},
                new SteelWorkBreakdownLine{ComponentName="Mobile crane, erection", Quantity=craneHours, Unit="hr/Tonne", UnitPrice=craneCost, TotalPrice=craneTotal},
                new SteelWorkBreakdownLine{ComponentName="Total Plant", TotalPrice=plantTotal},

                new SteelWorkBreakdownLine{ComponentName="Total", TotalPrice=net},
            };

            return (net, lines);
        }

        private SteelworkItem StructuralItem(int itemNo, string materialName, string materialLabel,
                                             string description, bool perKg,
                                             double gangHours = 10, double craneHours = 1.5)
        {
            var (netPerTonne, lines) = StructuralSteelPerTonne(itemNo, materialName, materialLabel, gangHours, craneHours);

            // The kg rate is the tonne rate over 1000, never a separate build-up,
            // so the two cannot disagree.
            double net = perKg ? netPerTonne / 1000.0 : netPerTonne;
            if (perKg)
            {
                lines.Add(new SteelWorkBreakdownLine
                {
                    ComponentName = "Rate per kg (tonne rate / 1000)",
                    Quantity = 1000,
                    Unit = "kg/Tonne",
                    TotalPrice = net
                });
            }

            var ohp = ApplyOHP(net);
            return new SteelworkItem
            {
                ItemNo = itemNo,
                Description = description,
                Unit = perKg ? "kg" : "Tonne",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                SteelWorkBreakdownLines = lines
            };
        }

        private SteelworkItem ComputeItem4() => StructuralItem(4,
            "Universal beam (I section), supply", "Universal beam (I section)",
            "Universal beam (I section) in structural steelwork; supply, fabricate, hoist and erect, including cleats, base plates and bolted connections", false);

        private SteelworkItem ComputeItem5() => StructuralItem(5,
            "Universal beam (I section), supply", "Universal beam (I section)",
            "Universal beam (I section) in structural steelwork; supply, fabricate, hoist and erect, measured per kilogramme", true);

        private SteelworkItem ComputeItem6() => StructuralItem(6,
            "Universal column (H section), supply", "Universal column (H section)",
            "Universal column (H section) in structural steelwork; supply, fabricate, hoist and erect, including base plates, holding down bolts and bolted connections", false);

        private SteelworkItem ComputeItem7() => StructuralItem(7,
            "Universal column (H section), supply", "Universal column (H section)",
            "Universal column (H section) in structural steelwork; supply, fabricate, hoist and erect, measured per kilogramme", true);

        // Angles and channels are lighter work per tonne: more pieces, but simpler
        // connections and far less crane time than a main frame member.
        private SteelworkItem ComputeItem8() => StructuralItem(8,
            "Rolled steel angle, supply", "Rolled steel angle",
            "Rolled steel angle in bracing, purlins and secondary steelwork; supply, fabricate and fix", false, 12, 0.6);

        private SteelworkItem ComputeItem9() => StructuralItem(9,
            "Rolled steel angle, supply", "Rolled steel angle",
            "Rolled steel angle in bracing, purlins and secondary steelwork; supply, fabricate and fix, measured per kilogramme", true, 12, 0.6);

        private SteelworkItem ComputeItem10() => StructuralItem(10,
            "Rolled steel channel, supply", "Rolled steel channel",
            "Rolled steel channel in beams, runners and secondary steelwork; supply, fabricate, hoist and fix", false, 11, 1.0);

        private SteelworkItem ComputeItem11() => StructuralItem(11,
            "Steel plate, supply", "Steel plate",
            "Steel plate in gussets, cleats, base plates and stiffeners; supply, cut, drill and weld", false, 14, 0.5);

        // 12 ── Fillet weld, measured per metre of run
        private SteelworkItem ComputeItem12()
        {
            const int no = 12;
            double welderCost = (GetLabourRate("Welder") / 8) * 1.4;
            double labourerCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double weldPlantCost = GetLabourRate("Welding machine (big)") / 8;
            double electrodeCost = GetMaterialPrice("Steel plate, supply (per kg)");

            double runHours = UserRateEditStore.Current.Qty(SectionKey, no, "Welding gang", 0.35);
            double welderQty = UserRateEditStore.Current.Qty(SectionKey, no, "Welder", 1);
            double labourerQty = UserRateEditStore.Current.Qty(SectionKey, no, "Labourer", 1);
            // Electrode consumption for a 6mm fillet is roughly 0.35 kg per metre of
            // run. Priced off the plate row because the library has no electrode row.
            double electrodeQty = UserRateEditStore.Current.Qty(SectionKey, no, "Welding electrodes", 0.35);

            double gangPerHour = welderCost * welderQty + labourerCost * labourerQty;
            double labourTotal = gangPerHour * runHours;
            double plantTotal = weldPlantCost * runHours;
            double electrodeTotal = electrodeCost * electrodeQty;
            double net = electrodeTotal + labourTotal + plantTotal;

            var ohp = ApplyOHP(net);
            var lines = new ObservableCollection<SteelWorkBreakdownLine>
            {
                new SteelWorkBreakdownLine{ComponentName="Welding electrodes", Quantity=electrodeQty, Unit="kg/m", UnitPrice=electrodeCost, TotalPrice=electrodeTotal},
                new SteelWorkBreakdownLine{ComponentName="Welder", Quantity=welderQty, Unit="N/hr", UnitPrice=welderCost, TotalPrice=welderCost*welderQty},
                new SteelWorkBreakdownLine{ComponentName="Labourer", Quantity=labourerQty, Unit="N/hr", UnitPrice=labourerCost, TotalPrice=labourerCost*labourerQty},
                new SteelWorkBreakdownLine{ComponentName="Gang Cost per hour", TotalPrice=gangPerHour},
                new SteelWorkBreakdownLine{ComponentName="Welding gang", Quantity=runHours, Unit="hr/m", TotalPrice=labourTotal},
                new SteelWorkBreakdownLine{ComponentName="Welding machine (big)", Quantity=runHours, Unit="hr", UnitPrice=weldPlantCost, TotalPrice=plantTotal},
                new SteelWorkBreakdownLine{ComponentName="Total", TotalPrice=net},
            };

            return new SteelworkItem
            {
                ItemNo = no,
                Description = "6mm continuous fillet weld to structural steelwork; prepared, welded and dressed",
                Unit = "m",
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                SteelWorkBreakdownLines = lines
            };
        }

    }
}
