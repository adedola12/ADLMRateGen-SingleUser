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
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.SteelWork;

namespace ADLMRateGen.ViewModel.Painting
{
    public class PaintWorkViewModel : ViewModelBase
    {
        private readonly GetItemsFromDB _helper;

        // OPTIONAL: only needed if you actually use SteelWork values
        private readonly SteelWorkViewModel? _steelWorkViewModel;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        // ─── Sorting / filtering helpers ──────────────────────────────────────────────
        private bool _isNetCostFilterOn = false;
        private SortState _currentSort = SortState.None;
        private enum SortState { None, Overhead, TotalCost }

        private const string SectionKey = SectionKeys.Paint;

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

        public ObservableCollection<PaintWorkItem> PaintWorkItems { get; set; } =
            new ObservableCollection<PaintWorkItem>();

        public ICollectionView PaintWorkCollectionView { get; private set; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    RaisePropertyChanged();
                    PaintWorkCollectionView?.Refresh();
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

        public PaintWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib, SteelWorkViewModel? steelWorkVM = null)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);
            _steelWorkViewModel = steelWorkVM;

            // ✅ CollectionView first (safe even if rebuild runs early)
            PaintWorkCollectionView = CollectionViewSource.GetDefaultView(PaintWorkItems);
            PaintWorkCollectionView.Filter = FilterPaintworkItem;

            // Commands
            RecomputeCommand = new DelegateCommand(_ => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
            FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
            SortCommand = new DelegateCommand(_ => CycleSort());
            AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

            // listen for library changes
            matLib.LibraryChanged += OnLibraryChanged;
            labourLib.LibraryChanged += OnLibraryChanged;

            // ✅ Compute engine
            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);

            // ✅ ComputeCatalog: disk first then cloud
            ComputeCatalogStore.ReloadFromDisk();
            ComputeCatalogStore.Changed += OnLibraryChanged;
            _ = LoadComputeCatalogForSectionAsync();

            // ✅ RateLibrary: disk first then cloud  (THIS IS WHAT YOU NEED)
            RateLibraryStore.ReloadFromDisk();
            RateLibraryStore.Changed += OnLibraryChanged;
            _ = LoadRateLibraryAsync();

            // ✅ Initial build (shows disk cached items immediately)
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
                    var nos = PaintWorkItems.Select(i => i.ItemNo).ToList();
                    foreach (var n in nos) RecomputeItemInPlace(n);
                }
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp == null || disp.CheckAccess()) Refresh();
                else disp.BeginInvoke((Action)Refresh);
            };
        }

        /* -------------------- Cloud loaders -------------------- */

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

                OnLibraryChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] load failed: {ex}");
            }
        }

        /* -------------------- UI helpers -------------------- */

        private void RunOnUi(Action action)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null)
            {
                action();
                return;
            }

            if (disp.CheckAccess())
                action();
            else
                disp.Invoke(action);
        }

        private void OnLibraryChanged()
        {
            // Stores may raise Changed from background thread
            RunOnUi(RecomputeAll);
        }

        private bool FilterPaintworkItem(object obj)
        {
            if (obj is PaintWorkItem item)
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
            PaintWorkItems.Clear();
            BuildPaintworkItem();
            PaintWorkCollectionView?.Refresh();
        }

        /// <summary>
        /// Re-runs the matching ComputeItemN method for the supplied ItemNo and updates the
        /// existing Item instance in place so the open popup keeps its DataContext binding live.
        /// Called by the breakdown popup after a user edits a Quantity.
        /// </summary>
        public void RecomputeItemInPlace(int itemNo)
        {
            var existing = PaintWorkItems.FirstOrDefault(i => i.ItemNo == itemNo);
            if (existing == null) return;

            Func<PaintWorkItem>[] all =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5,
                ComputeItem6, ComputeItem7, ComputeItem8, ComputeItem9
            };

            PaintWorkItem? fresh = null;
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

            existing.PaintingBreakdownLines.Clear();
            foreach (var line in fresh.PaintingBreakdownLines)
                existing.PaintingBreakdownLines.Add(line);

            PaintWorkCollectionView?.Refresh();
        }

        private void ShowDetails(object o)
        {
            if (o is PaintWorkItem item)
            {
                var detailedControl = new PaintworkDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () => { SelectedDetail = null; };

                SelectedDetail = detailedControl;
            }
        }

        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            PaintWorkCollectionView.SortDescriptions.Clear();

            if (_isNetCostFilterOn)
                PaintWorkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(PaintWorkItem.NetCost), ListSortDirection.Ascending));
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

            PaintWorkCollectionView.SortDescriptions.Clear();

            switch (_currentSort)
            {
                case SortState.Overhead:
                    PaintWorkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(PaintWorkItem.OverheadValue), ListSortDirection.Ascending));
                    break;

                case SortState.TotalCost:
                    PaintWorkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(PaintWorkItem.TotalCost), ListSortDirection.Ascending));
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

        /* -------------------- Build items -------------------- */

        private void BuildPaintworkItem()
        {
            Func<PaintWorkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5,
                ComputeItem6, ComputeItem7, ComputeItem8, ComputeItem9
                // ComputeItem10, ComputeItem11, ComputeItem12
            };

            foreach (var compute in computeMethods)
                PaintWorkItems.Add(compute());

            // ✅ Dynamic compute defs (ComputeCatalogStore)
            AppendApiComputeItems();

            // ✅ Admin rates from cloud/disk (RateLibraryStore)
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
            int nextNo = PaintWorkItems.Count + 1;

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

                    var breakdown = new ObservableCollection<PaintingBreakdownLine>();

                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new PaintingBreakdownLine
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
                        breakdown.Add(new PaintingBreakdownLine
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
                        breakdown.Add(new PaintingBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new PaintingBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    PaintWorkItems.Add(new PaintWorkItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 0),
                        PaintingBreakdownLines = breakdown
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
                System.Diagnostics.Debug.WriteLine($"[RateLibrary] No rate items in store. File={RateLibraryStore.FilePath}");
                return;
            }

            int nextNo = PaintWorkItems.Count + 1;
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

                var breakdown = new ObservableCollection<PaintingBreakdownLine>();
                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        breakdown.Add(new PaintingBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice))
                        });
                    }
                }

                PaintWorkItems.Add(new PaintWorkItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 0),
                    PaintingBreakdownLines = breakdown
                });

                appended++;
            }

            System.Diagnostics.Debug.WriteLine($"[RateLibrary] Appended {appended} admin rate(s) for section '{SectionKey}'.");
        }

        /* -------------------- Shared helpers -------------------- */

        private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
        {
            double ov = netCost * (OverheadPercent / 100);
            double pv = netCost * (ProfitPercent / 100);
            double total = netCost + ov + pv;
            return (ov, pv, total);
        }

        private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
        private double GetLabourRate(string name) => _helper.GetLabourRate(name);

        public double GetSteelNetValue(Func<SteelworkItem> computeFunc)
        {
            // safe if not injected
            return _steelWorkViewModel == null ? 0 : _steelWorkViewModel.GetSteelNetValue(computeFunc);
        }

        public double GetNetValue(Func<PaintWorkItem> computeItemFunc)
        {
            var item = computeItemFunc();
            return item.NetCost;
        }

        /* -------------------- YOUR COMPUTE METHODS --------------------
           Keep all your ComputeItem1..ComputeItem12 exactly as you already have.
           (No changes required.)
        */

        #region COMPUTE METHOD
        private PaintWorkItem ComputeItem1()
        {
            //MATERIAL CLASS
            double puttyCost = GetMaterialPrice("Poly Filla 1.8Kg Pack.");
            double antiFungalCost = GetMaterialPrice("Pealux Anti Fungi/Algae Emulsion")/4;
            double texcoteCost = GetMaterialPrice("Peacotex Textured Finish (B/W)") /25;

            double puttyQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Putty filler to joints and fine cracks", 0.3);
            double antiFungalQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Anti-fungal paint", 0.4);
            double texcoteQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Texcote-(Pealux - 25kg drum)", 1);

            double puttyRate = puttyCost * puttyQty;
            double antiFungalRate = antiFungalCost * antiFungalQty;
            double texcoteRate = texcoteCost * texcoteQty;

            double puttyWastePer = UserRateEditStore.Current.Qty(SectionKey, 1, "Add waste", 2.5);
            double antiFungalWastePer = UserRateEditStore.Current.Qty(SectionKey, 1, "Add waste", 10);
            double texcoatWastePer = UserRateEditStore.Current.Qty(SectionKey, 1, "Add waste", 10);

            double puttyWaste = puttyRate * (puttyWastePer / 100);
            double antiFungalWaste = antiFungalRate *(antiFungalWastePer / 100);
            double texcoatWaste = texcoteRate * (texcoatWastePer / 100);

            double materialTotal = puttyRate + antiFungalRate + texcoteRate +
                puttyWaste + antiFungalWaste + texcoatWaste;

            //LABOUR COST
            double painterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double painterQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Painter", 2);
            double painterRate = painterCost * painterQty;

            double painterOutput = UserRateEditStore.Current.Qty(SectionKey, 1, "Output", 18);

            double painterOutputPerSqm = painterRate / painterOutput;

            double netCostPerSqm = materialTotal + painterOutputPerSqm;
            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Putty filler to joints and fine cracks", Quantity=puttyQty, Unit="kg/m2",
                    UnitPrice= puttyCost, TotalPrice=puttyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=puttyWastePer, Unit="%",
                    TotalPrice=puttyWaste},
                new PaintingBreakdownLine{ ComponentName="Anti-fungal paint", Quantity=antiFungalQty, Unit="Lit/m2",
                    UnitPrice= antiFungalCost, TotalPrice=antiFungalRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=antiFungalWastePer, Unit="%",
                    TotalPrice=antiFungalWaste},
                new PaintingBreakdownLine{ ComponentName="Texcote-(Pealux - 25kg drum)", Quantity=texcoteQty, Unit="kg/m2",
                    UnitPrice= texcoteCost, TotalPrice=texcoteRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=texcoatWastePer, Unit="%",
                    TotalPrice=texcoatWaste},
                new PaintingBreakdownLine{ComponentName="Total Material", TotalPrice=materialTotal},

                new PaintingBreakdownLine{ComponentName="Painter", Quantity=painterQty, Unit="per/day", UnitPrice=painterCost,
                    TotalPrice=painterRate},
                new PaintingBreakdownLine{ComponentName="Output", Quantity=painterOutput, Unit="m2/day",
                    UnitPrice=painterRate, TotalPrice= painterOutputPerSqm},


                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 1,
                Description = "Prepare and apply one undercoat anti-fungal paint and " +
                "one finish coat white texcote (peacock) paint to wall not exceeding " +
                "4.00m from ground level, on blockwork or concrete externally.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };

        }
        private PaintWorkItem ComputeItem2()
        {
            //MATERIAL CLASS
            double puttyCost = GetMaterialPrice("Poly Filla 1.8Kg Pack.");
            //double antiFungalCost = GetMaterialPrice("Pealux Anti Fungi/Algae Emulsion") / 4;
            double texcoteCost = GetMaterialPrice("Peacotex Textured Finish (B/W)") / 25;

            double puttyQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Putty filler to joints and fine cracks", 0.3);
            //double antiFungalQty = 0.4;
            double texcoteQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Texcote-(Pealux - 25kg drum)", 1);

            double puttyRate = puttyCost * puttyQty;
            //double antiFungalRate = antiFungalCost * antiFungalQty;
            double texcoteRate = texcoteCost * texcoteQty;

            double puttyWastePer = UserRateEditStore.Current.Qty(SectionKey, 2, "Add waste", 2.5);
            //double antiFungalWastePer = 10;
            double texcoatWastePer = UserRateEditStore.Current.Qty(SectionKey, 2, "Add waste", 10);

            double puttyWaste = puttyRate * (puttyWastePer / 100);
            //double antiFungalWaste = antiFungalRate * (antiFungalWastePer / 100);
            double texcoatWaste = texcoteRate * (texcoatWastePer / 100);

            double materialTotal = puttyRate  + texcoteRate +
                puttyWaste  + texcoatWaste;

            //LABOUR COST
            double painterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double painterQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Painter", 2);
            double painterRate = painterCost * painterQty;

            double painterOutput = UserRateEditStore.Current.Qty(SectionKey, 2, "Output", 30);

            double painterOutputPerSqm = painterRate / painterOutput;

            double netCostPerSqm = materialTotal + painterOutputPerSqm;
            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Putty filler to joints and fine cracks", Quantity=puttyQty, Unit="kg/m2",
                    UnitPrice= puttyCost, TotalPrice=puttyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=puttyWastePer, Unit="%",
                    TotalPrice=puttyWaste},
                //new PaintingBreakdownLine{ ComponentName="Anti-fungal paint", Quantity=antiFungalQty, Unit="Lit/m2",
                //    UnitPrice= antiFungalCost, TotalPrice=antiFungalRate},
                //new PaintingBreakdownLine{ComponentName="Add waste", Quantity=antiFungalWastePer, Unit="%",
                //    TotalPrice=antiFungalWaste},
                new PaintingBreakdownLine{ ComponentName="Texcote-(Pealux - 25kg drum)", Quantity=texcoteQty, Unit="kg/m2",
                    UnitPrice= texcoteCost, TotalPrice=texcoteRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=texcoatWastePer, Unit="%",
                    TotalPrice=texcoatWaste},
                new PaintingBreakdownLine{ComponentName="Total Material", TotalPrice=materialTotal},

                new PaintingBreakdownLine{ComponentName="Painter", Quantity=painterQty, Unit="per/day", UnitPrice=painterCost,
                    TotalPrice=painterRate},
                new PaintingBreakdownLine{ComponentName="Output", Quantity=painterOutput, Unit="m2/day",
                    UnitPrice=painterRate, TotalPrice= painterOutputPerSqm},


                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 2,
                Description = "Prepare and apply white texcote (peacock) paint to wall " +
                "not exceeding 4.00m from ground level internally.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };

        }
        private PaintWorkItem ComputeItem3()
        {
            //MATERIAL CLASS
            double puttyCost = GetMaterialPrice("Poly Filla 1.8Kg Pack.");
            double primingCost = GetMaterialPrice("White Emulsion (High Quality)") / 4;
            double emulsionCost = GetMaterialPrice("White Emulsion (High Quality)") / 4;

            double puttyQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Putty filler to joints and fine cracks", 0.3);
            double primingQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Priming paint", 0.28);
            double emulsionQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Emulsion Paint - (Peacock)", 0.42);

            double puttyRate = puttyCost * puttyQty;
            double primingRate = primingCost * primingQty;
            double emulsionRate = emulsionCost * emulsionQty;

            double puttyWastePer = UserRateEditStore.Current.Qty(SectionKey, 3, "Add waste", 2.5);
            double primingWastePer = UserRateEditStore.Current.Qty(SectionKey, 3, "Add waste", 10);
            double emulsionWastePer = UserRateEditStore.Current.Qty(SectionKey, 3, "Add waste", 10);

            double puttyWaste = puttyRate * (puttyWastePer / 100);
            double primingWaste = primingRate * (primingWastePer / 100);
            double emulsionWaste = emulsionRate * (emulsionWastePer / 100);

            double materialTotal = puttyRate + primingRate + emulsionRate +
                puttyWaste + primingWaste + emulsionWaste;

            //LABOUR COST
            double painterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double painterQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Painter", 2);
            double painterRate = painterCost * painterQty;

            double painterOutput = UserRateEditStore.Current.Qty(SectionKey, 3, "Output", 20);

            double painterOutputPerSqm = painterRate / painterOutput;

            double netCostPerSqm = materialTotal + painterOutputPerSqm;
            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Putty filler to joints and fine cracks", Quantity=puttyQty, Unit="kg/m2",
                    UnitPrice= puttyCost, TotalPrice=puttyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=puttyWastePer, Unit="%",
                    TotalPrice=puttyWaste},
                new PaintingBreakdownLine{ ComponentName="Priming paint", Quantity=primingQty, Unit="Lit/m2",
                    UnitPrice= primingCost, TotalPrice=primingRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=primingWastePer, Unit="%",
                    TotalPrice=primingWaste},
                new PaintingBreakdownLine{ ComponentName="Emulsion Paint - (Peacock)", Quantity=emulsionQty, Unit="Lit/m2",
                    UnitPrice= emulsionCost, TotalPrice=emulsionRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=emulsionWastePer, Unit="%",
                    TotalPrice=emulsionWaste},
                new PaintingBreakdownLine{ComponentName="Total Material", TotalPrice=materialTotal},

                new PaintingBreakdownLine{ComponentName="Painter", Quantity=painterQty, Unit="per/day", UnitPrice=painterCost,
                    TotalPrice=painterRate},
                new PaintingBreakdownLine{ComponentName="Output", Quantity=painterOutput, Unit="m2/day",
                    UnitPrice=painterRate, TotalPrice= painterOutputPerSqm},


                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 3,
                Description = "Prepare and apply one undercoat and two finish coats quality, white emulsion  paint, to wall not exceeding 4.00m from ground level internally.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }
        private PaintWorkItem ComputeItem4()
        {
            double chemicalCost = GetMaterialPrice("Oil and Grease Remover (Amercoat 57 OC)");
            double chemicalLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            double chemicalQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Material cost per square metre of degreasing chemical.", 0.2);
            double chemicalLabourQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour application", 0.08);

            double chemicalRate = chemicalCost * chemicalQty;
            double chemicalLabourRate = chemicalLabourCost * chemicalLabourQty;
            double chemicalTotal = chemicalRate + chemicalLabourRate;

            //Blasting
            double compressorCost = GetLabourRate("Compressor")/8;
            double fuelCost = GetMaterialPrice("Diesel");
            double sandPotCost = GetLabourRate("Sand Pot for sand blasting")/8;
            double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
            double gritCost = GetMaterialPrice("Grit (for sand blasting)");

            double compressorQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Compressor", 0.025);
            double fuelQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Fuel (Diesel)", 45);
            double sandPotQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Sand Pot", 0.025);
            double respiratoryQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Respiratory gear.", 0.025);
            double gritQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Grit", 0.15);
            double oilPer = UserRateEditStore.Current.Qty(SectionKey, 4, "Oil and consumables (per day)", 3);

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;

            double blastingOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Blasting operator.", 1);
            double blastingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour (for loading sand pot)", 2);

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate = blastingLabourCost* blastingLabouurQty;
            double blastingLabour = blastingOperatorRate + blastingLabourRate;
            double blastingOutputDaily = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour Output", 300);

            double blastingPerSqm = blastingLabour / blastingOutputDaily;

            double totalBlastingPerDay = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

            //Primer Application
            double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
            double sprayingLabourCost = (GetLabourRate("Skilled/Artisan")/8) *1.4;
            double primerCost = GetMaterialPrice("Zinc Rich Epoxy (Amercoat 64, Pale Red)");

            double sprayingMachineQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Spraying machine", 0.10);
            double sprayingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour spraying - spray painter", 0.1);
            double primerQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Primer coat - Zinc rich epoxy", 0.11);
            double primerWastePer = UserRateEditStore.Current.Qty(SectionKey, 4, "Add waste", 5);

            double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
            double sprayingLabourRate = sprayingLabourCost* sprayingLabouurQty;
            double primerRate = primerCost* primerQty;
            double primerWaste = primerRate * (primerWastePer / 100);

            double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

            //Undercoat Application
            double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

            double undercoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Spraying machine", 0.10);
            double undercoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour spraying - spray painter", 0.10);
            double epoxyQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Build coat - High build epoxy", 0.13);
            double epoxyWastePer = UserRateEditStore.Current.Qty(SectionKey, 4, "Add waste", 5);

            double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
            double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
            double epoxyRate = epoxyCost * epoxyQty;
            double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

            double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

            //FinishCoat Application
            double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double enamelCost = GetMaterialPrice("Mobil Beige Epoxy Enamel")/4;

            double finishcoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Spraying machine", 0.10);
            double finishcoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Labour spraying - spray painter", 0.10);
            double enamelQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Finish coat - Mobil beige gloss", 0.08);
            double enamelWastePer = UserRateEditStore.Current.Qty(SectionKey, 4, "Add waste", 5);

            double finishcoatMachineRate = finishcoatMachineCost * finishcoatMachineQty;
            double finishcoatLabourRate = finishcoatLabourCost * finishcoatLabouurQty;
            double enamelRate = enamelCost * enamelQty;
            double enamelWaste = enamelRate * (enamelWastePer / 100);

            double totalFinishCoat = finishcoatMachineRate + finishcoatLabourRate + enamelRate + enamelWaste;

            double netCostPerSqm = totalBlastingPerDay + totalPrimer + totalUnderCoat + totalFinishCoat + chemicalTotal;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Material cost per square metre of degreasing chemical.", Quantity=chemicalQty, Unit="lit/m2",
                    UnitPrice= chemicalCost, TotalPrice=chemicalRate},
                new PaintingBreakdownLine{ ComponentName="Labour application", Quantity=chemicalLabourQty, Unit="hr/m2",
                    UnitPrice= chemicalLabourCost, TotalPrice=chemicalLabourRate},
                new PaintingBreakdownLine{ComponentName="Total Chemical Material", TotalPrice=chemicalTotal},

                new PaintingBreakdownLine{ ComponentName="Compressor", Quantity=compressorQty, Unit="hr/m2",
                    UnitPrice= compressorCost, TotalPrice=compressorRate},
                new PaintingBreakdownLine{ ComponentName="Fuel (Diesel)", Quantity=fuelQty, Unit="lit/day",
                    UnitPrice= fuelCost, TotalPrice=fuelRate},
                new PaintingBreakdownLine{ComponentName="Oil and consumables (per day)", Quantity=oilPer, Unit="%",
                    TotalPrice=oilRate},
                new PaintingBreakdownLine{ ComponentName="Sand Pot", Quantity=sandPotQty, Unit="hr/m2",
                    UnitPrice= sandPotCost, TotalPrice=sandPotRate},
                new PaintingBreakdownLine{ ComponentName="Respiratory gear.", Quantity=respiratoryQty, Unit="hr/m2",
                    UnitPrice= respiratoryCost, TotalPrice=respiratoryRate},
                new PaintingBreakdownLine{ ComponentName="Grit", Quantity=gritQty, Unit="m3/m2",
                    UnitPrice= gritCost, TotalPrice=gritRate},

                new PaintingBreakdownLine{ ComponentName="Blasting operator.", Quantity=blastingOperatorQty, Unit="per/day",
                    UnitPrice= blastingOperatorCost, TotalPrice=blastingOperatorRate},
                new PaintingBreakdownLine{ ComponentName="Labour (for loading sand pot)", Quantity=blastingLabouurQty, Unit="per/day",
                    UnitPrice= blastingLabourCost, TotalPrice=blastingLabourRate},
                new PaintingBreakdownLine{ComponentName="Labour Output", Quantity=blastingOutputDaily, Unit="m2/day", UnitPrice=blastingLabour,
                    TotalPrice=blastingPerSqm},
                new PaintingBreakdownLine{ComponentName="Total Blasting ", TotalPrice=totalBlastingPerDay},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=sprayingMachineQty, Unit="hr/m2",
                    UnitPrice= sprayingMachineCost, TotalPrice=sprayingMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=sprayingLabouurQty, Unit="hr/m2",
                    UnitPrice= sprayingLabourCost, TotalPrice=sprayingLabourRate},
                new PaintingBreakdownLine{ ComponentName="Primer coat - Zinc rich epoxy", Quantity=primerQty, Unit="lit/m2",
                    UnitPrice= primerCost, TotalPrice=primerRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=primerWastePer, Unit="%",
                    TotalPrice=primerWaste},
                new PaintingBreakdownLine{ComponentName="Total Priming ", TotalPrice=totalPrimer},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=undercoatMachineQty, Unit="hr/m2",
                    UnitPrice= undercoatMachineCost, TotalPrice=undercoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=undercoatLabouurQty, Unit="hr/m2",
                    UnitPrice= undercoatLabourCost, TotalPrice=undercoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="Build coat - High build epoxy", Quantity=epoxyQty, Unit="lit/m2",
                    UnitPrice= epoxyCost, TotalPrice=epoxyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=epoxyWastePer, Unit="%",
                    TotalPrice=epoxyWaste},
                new PaintingBreakdownLine{ComponentName="Total Undercoat ", TotalPrice=totalUnderCoat},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=finishcoatMachineQty, Unit="hr/m2",
                    UnitPrice= finishcoatMachineCost, TotalPrice=finishcoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=finishcoatLabouurQty, Unit="hr/m2",
                    UnitPrice= finishcoatLabourCost, TotalPrice=finishcoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="Finish coat - Mobil beige gloss", Quantity=enamelQty, Unit="lit/m2",
                    UnitPrice= enamelCost, TotalPrice=enamelRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=enamelWastePer, Unit="%",
                    TotalPrice=enamelWaste},
                new PaintingBreakdownLine{ComponentName="Total Finalcoat ", TotalPrice=totalFinishCoat},

                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 4,
                Description = "Prepare  steel surface  to Mobil SP10, cleaning surface of grease, sand-blasting and applying high build epoxy priming coat and Mobil beige finish coat -(Ameron Paints)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }
        private PaintWorkItem ComputeItem5()
        {
            //double chemicalCost = GetMaterialPrice("Oil and Grease Remover (Amercoat 57 OC)");
            //double chemicalLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;

            //double chemicalQty = 0.2;
            //double chemicalLabourQty = 0.08;

            //double chemicalRate = chemicalCost * chemicalQty;
            //double chemicalLabourRate = chemicalLabourCost * chemicalLabourQty;
            //double chemicalTotal = chemicalRate + chemicalLabourRate;

            //Blasting
            double compressorCost = GetLabourRate("Compressor") / 8;
            double fuelCost = GetMaterialPrice("Diesel");
            double sandPotCost = GetLabourRate("Sand Pot for sand blasting") / 8;
            double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
            double gritCost = GetMaterialPrice("Grit (for sand blasting)");

            double compressorQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Compressor", 0.025);
            double fuelQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Fuel (Diesel)", 45);
            double sandPotQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Sand Pot", 0.025);
            double respiratoryQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Respiratory gear.", 0.025);
            double gritQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Grit", 0.15);
            double oilPer = UserRateEditStore.Current.Qty(SectionKey, 5, "Oil and consumables (per day)", 3);

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;

            double blastingOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Blasting operator.", 1);
            double blastingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour (for loading sand pot)", 2);

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
            double blastingLabour = blastingOperatorRate + blastingLabourRate;
            double blastingOutputDaily = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour Output", 300);

            double blastingPerSqm = blastingLabour / blastingOutputDaily;

            double totalBlastingPerDay = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

            //Base coat Application
            double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
            double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double primerCost = GetMaterialPrice("Inorganic Zinc Silicate (Dimetcote 6, Redish Grey)");

            double sprayingMachineQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Spraying machine", 0.10);
            double sprayingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour spraying - spray painter", 0.10);
            double primerQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Base coat - Inorganic Zinc Silicate", 0.11);
            double primerWastePer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add waste", 5);

            double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
            double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
            double primerRate = primerCost * primerQty;
            double primerWaste = primerRate * (primerWastePer / 100);

            double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

            //First coat Application
            double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)") + GetMaterialPrice("Amercoat 8");

            double undercoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Spraying machine", 0.10);
            double undercoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour spraying - spray painter", 0.10);
            double epoxyQty = UserRateEditStore.Current.Qty(SectionKey, 5, "First coat - Thinned high build epoxy", 0.11);
            double epoxyWastePer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add waste", 5);

            double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
            double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
            double epoxyRate = epoxyCost * epoxyQty;
            double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

            double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

            //Second Coat Application
            double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double enamelCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

            double finishcoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Spraying machine", 0.10);
            double finishcoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour spraying - spray painter", 0.10);
            double enamelQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Second coat - High build epoxy", 0.14);
            double enamelWastePer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add waste", 5);

            double finishcoatMachineRate = finishcoatMachineCost * finishcoatMachineQty;
            double finishcoatLabourRate = finishcoatLabourCost * finishcoatLabouurQty;
            double enamelRate = enamelCost * enamelQty;
            double enamelWaste = enamelRate * (enamelWastePer / 100);

            double totalFinishCoat = finishcoatMachineRate + finishcoatLabourRate + enamelRate + enamelWaste;

            //Top Coat Application
            double topcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double topcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double beigeCost = GetMaterialPrice("Mobil Beige Epoxy Enamel") / 4;

            double topcoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Spraying machine", 0.10);
            double topcoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Labour spraying - spray painter", 0.10);
            double beigeQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Top coat - Mobil beige Urethane Enamel", 0.06);
            double beigeWastePer = UserRateEditStore.Current.Qty(SectionKey, 5, "Add waste", 5);

            double topcoatMachineRate = topcoatMachineCost * topcoatMachineQty;
            double topcoatLabourRate = topcoatLabourCost * topcoatLabouurQty;
            double beigeRate = beigeCost * beigeQty;
            double beigeWaste = beigeRate * (beigeWastePer / 100);

            double totalTopCoat = topcoatMachineRate + topcoatLabourRate + beigeRate + beigeWaste;

            double netCostPerSqm = totalBlastingPerDay + totalPrimer + totalUnderCoat + totalFinishCoat + totalTopCoat;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
				//new PaintingBreakdownLine{ ComponentName="Material cost per square metre of degreasing chemical.", Quantity=chemicalQty, Unit="lit/m2",
				//	UnitPrice= chemicalCost, TotalPrice=chemicalRate},
				//new PaintingBreakdownLine{ ComponentName="Labour application", Quantity=chemicalLabourQty, Unit="hr/m2",
				//	UnitPrice= chemicalLabourCost, TotalPrice=chemicalLabourRate},
				//new PaintingBreakdownLine{ComponentName="Total Chemical Material", TotalPrice=chemicalTotal},

				new PaintingBreakdownLine{ ComponentName="Compressor", Quantity=compressorQty, Unit="hr/m2",
                    UnitPrice= compressorCost, TotalPrice=compressorRate},
                new PaintingBreakdownLine{ ComponentName="Fuel (Diesel)", Quantity=fuelQty, Unit="lit/day",
                    UnitPrice= fuelCost, TotalPrice=fuelRate},
                new PaintingBreakdownLine{ComponentName="Oil and consumables (per day)", Quantity=oilPer, Unit="%",
                    TotalPrice=oilRate},
                new PaintingBreakdownLine{ ComponentName="Sand Pot", Quantity=sandPotQty, Unit="hr/m2",
                    UnitPrice= sandPotCost, TotalPrice=sandPotRate},
                new PaintingBreakdownLine{ ComponentName="Respiratory gear.", Quantity=respiratoryQty, Unit="hr/m2",
                    UnitPrice= respiratoryCost, TotalPrice=respiratoryRate},
                new PaintingBreakdownLine{ ComponentName="Grit", Quantity=gritQty, Unit="m3/m2",
                    UnitPrice= gritCost, TotalPrice=gritRate},

                new PaintingBreakdownLine{ ComponentName="Blasting operator.", Quantity=blastingOperatorQty, Unit="per/day",
                    UnitPrice= blastingOperatorCost, TotalPrice=blastingOperatorRate},
                new PaintingBreakdownLine{ ComponentName="Labour (for loading sand pot)", Quantity=blastingLabouurQty, Unit="per/day",
                    UnitPrice= blastingLabourCost, TotalPrice=blastingLabourRate},
                new PaintingBreakdownLine{ComponentName="Labour Output", Quantity=blastingOutputDaily, Unit="m2/day", UnitPrice=blastingLabour,
                    TotalPrice=blastingPerSqm},
                new PaintingBreakdownLine{ComponentName="Total Blasting ", TotalPrice=totalBlastingPerDay},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=sprayingMachineQty, Unit="hr/m2",
                    UnitPrice= sprayingMachineCost, TotalPrice=sprayingMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=sprayingLabouurQty, Unit="hr/m2",
                    UnitPrice= sprayingLabourCost, TotalPrice=sprayingLabourRate},
                new PaintingBreakdownLine{ ComponentName="Base coat - Inorganic Zinc Silicate", Quantity=primerQty, Unit="lit/m2",
                    UnitPrice= primerCost, TotalPrice=primerRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=primerWastePer, Unit="%",
                    TotalPrice=primerWaste},
                new PaintingBreakdownLine{ComponentName="Total Basecoat ", TotalPrice=totalPrimer},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=undercoatMachineQty, Unit="hr/m2",
                    UnitPrice= undercoatMachineCost, TotalPrice=undercoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=undercoatLabouurQty, Unit="hr/m2",
                    UnitPrice= undercoatLabourCost, TotalPrice=undercoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="First coat - Thinned high build epoxy", Quantity=epoxyQty, Unit="lit/m2",
                    UnitPrice= epoxyCost, TotalPrice=epoxyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=epoxyWastePer, Unit="%",
                    TotalPrice=epoxyWaste},
                new PaintingBreakdownLine{ComponentName="Total Firstcoat ", TotalPrice=totalUnderCoat},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=finishcoatMachineQty, Unit="hr/m2",
                    UnitPrice= finishcoatMachineCost, TotalPrice=finishcoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=finishcoatLabouurQty, Unit="hr/m2",
                    UnitPrice= finishcoatLabourCost, TotalPrice=finishcoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="Second coat - High build epoxy", Quantity=enamelQty, Unit="lit/m2",
                    UnitPrice= enamelCost, TotalPrice=enamelRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=enamelWastePer, Unit="%",
                    TotalPrice=enamelWaste},
                new PaintingBreakdownLine{ComponentName="Total Secondcoat ", TotalPrice=totalFinishCoat},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=topcoatMachineQty, Unit="hr/m2",
                    UnitPrice= topcoatMachineCost, TotalPrice=topcoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=topcoatLabouurQty, Unit="hr/m2",
                    UnitPrice= topcoatLabourCost, TotalPrice=topcoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="Top coat - Mobil beige Urethane Enamel", Quantity=beigeQty, Unit="lit/m2",
                    UnitPrice= beigeCost, TotalPrice=beigeRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=beigeWastePer, Unit="%",
                    TotalPrice=beigeWaste},
                new PaintingBreakdownLine{ComponentName="Total Topcoat ", TotalPrice=totalTopCoat},

                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 5,
                Description = "Prepare surface of steel to SP10, apply zinc silicate as base coat, thinned high build epoxy as first coat, " +
                "high build epoxy as second coat and urethane enamel as top coat, all works completed as specified. (Mobil EPG 35-B-70 Rev Oct 1996)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }
        private PaintWorkItem ComputeItem6()
        {
            //Blasting
            double compressorCost = GetLabourRate("Compressor") / 8;
            double fuelCost = GetMaterialPrice("Diesel");
            double sandPotCost = GetLabourRate("Sand Pot for sand blasting") / 8;
            double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
            double gritCost = GetMaterialPrice("Grit (for sand blasting)");

            double compressorQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Compressor", 0.025);
            double fuelQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Fuel (Diesel)", 45);
            double sandPotQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Sand Pot", 0.025);
            double respiratoryQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Respiratory gear.", 0.025);
            double gritQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Grit", 0.15);
            double oilPer = UserRateEditStore.Current.Qty(SectionKey, 6, "Oil and consumables (per day)", 3);

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;

            double blastingOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Blasting operator.", 1);
            double blastingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Labour (for loading sand pot)", 2);

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
            double blastingLabour = blastingOperatorRate + blastingLabourRate;
            double blastingOutputDaily = UserRateEditStore.Current.Qty(SectionKey, 6, "Labour Output", 300);

            double blastingPerSqm = blastingLabour / blastingOutputDaily;

            double totalBlastingPerDay = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

            //Base coat Application
            double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
            double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double primerCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

            double sprayingMachineQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Spraying machine", 0.10);
            double sprayingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Labour spraying - spray painter", 0.10);
            double primerQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Base coat - Inorganic Zinc Silicate", 0.52);
            double primerWastePer = UserRateEditStore.Current.Qty(SectionKey, 6, "Add waste", 5);

            double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
            double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
            double primerRate = primerCost * primerQty;
            double primerWaste = primerRate * (primerWastePer / 100);

            double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

            //First coat Application
            double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

            double undercoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Spraying machine", 0.10);
            double undercoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Labour spraying - spray painter", 0.10);
            double epoxyQty = UserRateEditStore.Current.Qty(SectionKey, 6, "First coat - Thinned high build epoxy", 0.52);
            double epoxyWastePer = UserRateEditStore.Current.Qty(SectionKey, 6, "Add waste", 5);

            double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
            double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
            double epoxyRate = epoxyCost * epoxyQty;
            double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

            double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

            ////Second Coat Application
            //double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            //double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            //double enamelCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

            //double finishcoatMachineQty = 0.10;
            //double finishcoatLabouurQty = 0.10;
            //double enamelQty = 0.14;
            //double enamelWastePer = 5;

            //double finishcoatMachineRate = finishcoatMachineCost * finishcoatMachineQty;
            //double finishcoatLabourRate = finishcoatLabourCost * finishcoatLabouurQty;
            //double enamelRate = enamelCost * enamelQty;
            //double enamelWaste = enamelRate * (enamelWastePer / 100);

            //double totalFinishCoat = finishcoatMachineRate + finishcoatLabourRate + enamelRate + enamelWaste;

            ////Top Coat Application
            //double topcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            //double topcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            //double beigeCost = GetMaterialPrice("Mobil Beige Epoxy Enamel") / 4;

            //double topcoatMachineQty = 0.10;
            //double topcoatLabouurQty = 0.10;
            //double beigeQty = 0.06;
            //double beigeWastePer = 5;

            //double topcoatMachineRate = topcoatMachineCost * topcoatMachineQty;
            //double topcoatLabourRate = topcoatLabourCost * topcoatLabouurQty;
            //double beigeRate = beigeCost * beigeQty;
            //double beigeWaste = beigeRate * (beigeWastePer / 100);

            //double totalTopCoat = topcoatMachineRate + topcoatLabourRate + beigeRate + beigeWaste;

            double netCostPerSqm = totalBlastingPerDay + totalPrimer + totalUnderCoat;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
				//new PaintingBreakdownLine{ ComponentName="Material cost per square metre of degreasing chemical.", Quantity=chemicalQty, Unit="lit/m2",
				//	UnitPrice= chemicalCost, TotalPrice=chemicalRate},
				//new PaintingBreakdownLine{ ComponentName="Labour application", Quantity=chemicalLabourQty, Unit="hr/m2",
				//	UnitPrice= chemicalLabourCost, TotalPrice=chemicalLabourRate},
				//new PaintingBreakdownLine{ComponentName="Total Chemical Material", TotalPrice=chemicalTotal},

				new PaintingBreakdownLine{ ComponentName="Compressor", Quantity=compressorQty, Unit="hr/m2",
                    UnitPrice= compressorCost, TotalPrice=compressorRate},
                new PaintingBreakdownLine{ ComponentName="Fuel (Diesel)", Quantity=fuelQty, Unit="lit/day",
                    UnitPrice= fuelCost, TotalPrice=fuelRate},
                new PaintingBreakdownLine{ComponentName="Oil and consumables (per day)", Quantity=oilPer, Unit="%",
                    TotalPrice=oilRate},
                new PaintingBreakdownLine{ ComponentName="Sand Pot", Quantity=sandPotQty, Unit="hr/m2",
                    UnitPrice= sandPotCost, TotalPrice=sandPotRate},
                new PaintingBreakdownLine{ ComponentName="Respiratory gear.", Quantity=respiratoryQty, Unit="hr/m2",
                    UnitPrice= respiratoryCost, TotalPrice=respiratoryRate},
                new PaintingBreakdownLine{ ComponentName="Grit", Quantity=gritQty, Unit="m3/m2",
                    UnitPrice= gritCost, TotalPrice=gritRate},

                new PaintingBreakdownLine{ ComponentName="Blasting operator.", Quantity=blastingOperatorQty, Unit="per/day",
                    UnitPrice= blastingOperatorCost, TotalPrice=blastingOperatorRate},
                new PaintingBreakdownLine{ ComponentName="Labour (for loading sand pot)", Quantity=blastingLabouurQty, Unit="per/day",
                    UnitPrice= blastingLabourCost, TotalPrice=blastingLabourRate},
                new PaintingBreakdownLine{ComponentName="Labour Output", Quantity=blastingOutputDaily, Unit="m2/day", UnitPrice=blastingLabour,
                    TotalPrice=blastingPerSqm},
                new PaintingBreakdownLine{ComponentName="Total Blasting ", TotalPrice=totalBlastingPerDay},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=sprayingMachineQty, Unit="hr/m2",
                    UnitPrice= sprayingMachineCost, TotalPrice=sprayingMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=sprayingLabouurQty, Unit="hr/m2",
                    UnitPrice= sprayingLabourCost, TotalPrice=sprayingLabourRate},
                new PaintingBreakdownLine{ ComponentName="Base coat - Inorganic Zinc Silicate", Quantity=primerQty, Unit="lit/m2",
                    UnitPrice= primerCost, TotalPrice=primerRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=primerWastePer, Unit="%",
                    TotalPrice=primerWaste},
                new PaintingBreakdownLine{ComponentName="Total Basecoat ", TotalPrice=totalPrimer},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=undercoatMachineQty, Unit="hr/m2",
                    UnitPrice= undercoatMachineCost, TotalPrice=undercoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=undercoatLabouurQty, Unit="hr/m2",
                    UnitPrice= undercoatLabourCost, TotalPrice=undercoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="First coat - Thinned high build epoxy", Quantity=epoxyQty, Unit="lit/m2",
                    UnitPrice= epoxyCost, TotalPrice=epoxyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=epoxyWastePer, Unit="%",
                    TotalPrice=epoxyWaste},
                new PaintingBreakdownLine{ComponentName="Total Topcoat ", TotalPrice=totalUnderCoat},

				//new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=finishcoatMachineQty, Unit="hr/m2",
				//	UnitPrice= finishcoatMachineCost, TotalPrice=finishcoatMachineRate},
				//new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=finishcoatLabouurQty, Unit="hr/m2",
				//	UnitPrice= finishcoatLabourCost, TotalPrice=finishcoatLabourRate},
				//new PaintingBreakdownLine{ ComponentName="Second coat - High build epoxy", Quantity=enamelQty, Unit="lit/m2",
				//	UnitPrice= enamelCost, TotalPrice=enamelRate},
				//new PaintingBreakdownLine{ComponentName="Add waste", Quantity=enamelWastePer, Unit="%",
				//	TotalPrice=enamelWaste},
				//new PaintingBreakdownLine{ComponentName="Total Secondcoat ", TotalPrice=totalFinishCoat},

				//new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=topcoatMachineQty, Unit="hr/m2",
				//	UnitPrice= topcoatMachineCost, TotalPrice=topcoatMachineRate},
				//new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=topcoatLabouurQty, Unit="hr/m2",
				//	UnitPrice= topcoatLabourCost, TotalPrice=topcoatLabourRate},
				//new PaintingBreakdownLine{ ComponentName="Top coat - Mobil beige Urethane Enamel", Quantity=beigeQty, Unit="lit/m2",
				//	UnitPrice= beigeCost, TotalPrice=beigeRate},
				//new PaintingBreakdownLine{ComponentName="Add waste", Quantity=beigeWastePer, Unit="%",
				//	TotalPrice=beigeWaste},
				//new PaintingBreakdownLine{ComponentName="Total Topcoat ", TotalPrice=totalTopCoat},

				new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 6,
                Description = "Prepare surface of steel to SP5, coal tar epoxy as base coat, and coal tar epoxy as top coat, all works completed as specified. " +
                "(Mobil EPG 35-B-81 Rev Jan 1993)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }
        private PaintWorkItem ComputeItem7()
        {
            double chemicalCost = GetMaterialPrice("Oil and Grease Remover (Amercoat 57 OC)");
            double chemicalLabourCost = (GetLabourRate("Labourer") / 8) * 1.4 * 1.4;

            double chemicalQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Material cost per square metre of degreasing chemical.", 0.2);
            double chemicalLabourQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour application", 0.08);

            double chemicalRate = chemicalCost * chemicalQty;
            double chemicalLabourRate = chemicalLabourCost * chemicalLabourQty;
            double chemicalTotal = chemicalRate + chemicalLabourRate;

            //Blasting
            double compressorCost = GetLabourRate("Compressor") / 8;
            double fuelCost = GetMaterialPrice("Diesel");
            double sandPotCost = GetLabourRate("Sand Pot for sand blasting") / 8;
            double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
            double gritCost = GetMaterialPrice("Grit (for sand blasting)");

            double compressorQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Compressor", 0.025);
            double fuelQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Fuel (Diesel)", 45);
            double sandPotQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Sand Pot", 0.025);
            double respiratoryQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Respiratory gear.", 0.025);
            double gritQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Grit", 0.15);
            double oilPer = UserRateEditStore.Current.Qty(SectionKey, 7, "Oil and consumables (per day)", 3);

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;

            double blastingOperatorQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Blasting operator.", 1);
            double blastingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour (for loading sand pot)", 2);

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
            double blastingLabour = blastingOperatorRate + blastingLabourRate;
            double blastingOutputDaily = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour Output", 300);

            double blastingPerSqm = blastingLabour / blastingOutputDaily;

            double totalBlastingPerDay = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

            //Primer Application
            double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
            double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double primerCost = GetMaterialPrice("Zinc Rich Epoxy (Amercoat 64, Pale Red)");

            double sprayingMachineQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Spraying machine", 0.10);
            double sprayingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour spraying - spray painter", 0.1);
            double primerQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Primer coat - Zinc rich epoxy", 0.11);
            double primerWastePer = UserRateEditStore.Current.Qty(SectionKey, 7, "Add waste", 5);

            double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
            double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
            double primerRate = primerCost * primerQty;
            double primerWaste = primerRate * (primerWastePer / 100);

            double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

            //Undercoat Application
            double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

            double undercoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Spraying machine", 0.10);
            double undercoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour spraying - spray painter", 0.10);
            double epoxyQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Build coat - High build epoxy", 0.13);
            double epoxyWastePer = UserRateEditStore.Current.Qty(SectionKey, 7, "Add waste", 5);

            double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
            double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
            double epoxyRate = epoxyCost * epoxyQty;
            double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

            double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

            //FinishCoat Application
            double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
            double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double enamelCost = GetMaterialPrice("Ditto but OSHA S/Yellow (Finish Coating)");

            double finishcoatMachineQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Spraying machine", 0.10);
            double finishcoatLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Labour spraying - spray painter", 0.10);
            double enamelQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Finish coat - Aliphatic Polyurethane - Safety Yellow ", 0.09);
            double enamelWastePer = UserRateEditStore.Current.Qty(SectionKey, 7, "Add waste", 5);

            double finishcoatMachineRate = finishcoatMachineCost * finishcoatMachineQty;
            double finishcoatLabourRate = finishcoatLabourCost * finishcoatLabouurQty;
            double enamelRate = enamelCost * enamelQty;
            double enamelWaste = enamelRate * (enamelWastePer / 100);

            double totalFinishCoat = finishcoatMachineRate + finishcoatLabourRate + enamelRate + enamelWaste;

            double netCostPerSqm = totalBlastingPerDay + totalPrimer + totalUnderCoat + totalFinishCoat + chemicalTotal;

            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Material cost per square metre of degreasing chemical.", Quantity=chemicalQty, Unit="lit/m2",
                    UnitPrice= chemicalCost, TotalPrice=chemicalRate},
                new PaintingBreakdownLine{ ComponentName="Labour application", Quantity=chemicalLabourQty, Unit="hr/m2",
                    UnitPrice= chemicalLabourCost, TotalPrice=chemicalLabourRate},
                new PaintingBreakdownLine{ComponentName="Total Chemical Material", TotalPrice=chemicalTotal},

                new PaintingBreakdownLine{ ComponentName="Compressor", Quantity=compressorQty, Unit="hr/m2",
                    UnitPrice= compressorCost, TotalPrice=compressorRate},
                new PaintingBreakdownLine{ ComponentName="Fuel (Diesel)", Quantity=fuelQty, Unit="lit/day",
                    UnitPrice= fuelCost, TotalPrice=fuelRate},
                new PaintingBreakdownLine{ComponentName="Oil and consumables (per day)", Quantity=oilPer, Unit="%",
                    TotalPrice=oilRate},
                new PaintingBreakdownLine{ ComponentName="Sand Pot", Quantity=sandPotQty, Unit="hr/m2",
                    UnitPrice= sandPotCost, TotalPrice=sandPotRate},
                new PaintingBreakdownLine{ ComponentName="Respiratory gear.", Quantity=respiratoryQty, Unit="hr/m2",
                    UnitPrice= respiratoryCost, TotalPrice=respiratoryRate},
                new PaintingBreakdownLine{ ComponentName="Grit", Quantity=gritQty, Unit="m3/m2",
                    UnitPrice= gritCost, TotalPrice=gritRate},

                new PaintingBreakdownLine{ ComponentName="Blasting operator.", Quantity=blastingOperatorQty, Unit="per/day",
                    UnitPrice= blastingOperatorCost, TotalPrice=blastingOperatorRate},
                new PaintingBreakdownLine{ ComponentName="Labour (for loading sand pot)", Quantity=blastingLabouurQty, Unit="per/day",
                    UnitPrice= blastingLabourCost, TotalPrice=blastingLabourRate},
                new PaintingBreakdownLine{ComponentName="Labour Output", Quantity=blastingOutputDaily, Unit="m2/day", UnitPrice=blastingLabour,
                    TotalPrice=blastingPerSqm},
                new PaintingBreakdownLine{ComponentName="Total Blasting ", TotalPrice=totalBlastingPerDay},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=sprayingMachineQty, Unit="hr/m2",
                    UnitPrice= sprayingMachineCost, TotalPrice=sprayingMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=sprayingLabouurQty, Unit="hr/m2",
                    UnitPrice= sprayingLabourCost, TotalPrice=sprayingLabourRate},
                new PaintingBreakdownLine{ ComponentName="Primer coat - Zinc rich epoxy", Quantity=primerQty, Unit="lit/m2",
                    UnitPrice= primerCost, TotalPrice=primerRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=primerWastePer, Unit="%",
                    TotalPrice=primerWaste},
                new PaintingBreakdownLine{ComponentName="Total Priming ", TotalPrice=totalPrimer},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=undercoatMachineQty, Unit="hr/m2",
                    UnitPrice= undercoatMachineCost, TotalPrice=undercoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=undercoatLabouurQty, Unit="hr/m2",
                    UnitPrice= undercoatLabourCost, TotalPrice=undercoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="Build coat - High build epoxy", Quantity=epoxyQty, Unit="lit/m2",
                    UnitPrice= epoxyCost, TotalPrice=epoxyRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=epoxyWastePer, Unit="%",
                    TotalPrice=epoxyWaste},
                new PaintingBreakdownLine{ComponentName="Total Undercoat ", TotalPrice=totalUnderCoat},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=finishcoatMachineQty, Unit="hr/m2",
                    UnitPrice= finishcoatMachineCost, TotalPrice=finishcoatMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=finishcoatLabouurQty, Unit="hr/m2",
                    UnitPrice= finishcoatLabourCost, TotalPrice=finishcoatLabourRate},
                new PaintingBreakdownLine{ ComponentName="Finish coat - Aliphatic Polyurethane - Safety Yellow ", Quantity=enamelQty, Unit="lit/m2",
                    UnitPrice= enamelCost, TotalPrice=enamelRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=enamelWastePer, Unit="%",
                    TotalPrice=enamelWaste},
                new PaintingBreakdownLine{ComponentName="Total Finalcoat ", TotalPrice=totalFinishCoat},

                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 7,
                Description = "Prepare  steel surface  to Mobil SP10, cleaning surface of grease, sand-blasting and applying high build epoxy priming coat and Mobil beige " +
                "finish coat -(Ameron Paints)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }
        private PaintWorkItem ComputeItem8()
        {
            double scrubLabour = (GetLabourRate("Labourer") / 8) * 1.4;
            double scrubLabourQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Scrub wall down of all dirt. (Labour Only)", 0.2);
            double scrubLabourRate = scrubLabour * scrubLabourQty;

            double latexCost = GetMaterialPrice("Multipurpose Polymaide Epoxy (Amercoat 385, Off White)");
            double finishLatexCost = GetMaterialPrice("Ditto but Mobil Beige F38");

            double latexQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Exterior Acrylic Latex.", 0.4);
            double finishLatexQty = 0.4;

            double latexRate = latexCost * latexQty;
            double finishLatexRate = finishLatexCost * finishLatexQty;

            double waste = UserRateEditStore.Current.Qty(SectionKey, 8, "Add waste", 10);

            double latexWaste = latexRate * (waste / 100);
            double finishLatexWaste = finishLatexRate * (waste / 100);

            double latexTotal = latexRate + latexWaste;
            double finishLatexTotal = finishLatexRate + finishLatexWaste;

            double painterCost = GetLabourRate("Skilled/Artisan");
            double painterQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Painter", 1);
            double painterRate = painterCost * painterQty;

            double painterOutput = UserRateEditStore.Current.Qty(SectionKey, 8, "Output", 18);
            double painLabour = painterRate / painterOutput;

            double netCostPerSqm = scrubLabourRate + latexTotal + finishLatexTotal + painLabour;
            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Scrub wall down of all dirt. (Labour Only)", Quantity=scrubLabourQty, Unit="hr/m2",
                    UnitPrice= scrubLabour, TotalPrice=scrubLabourRate},

                new PaintingBreakdownLine{ ComponentName="Exterior Acrylic Latex.", Quantity=latexQty, Unit="Lit/m2",
                    UnitPrice= latexCost, TotalPrice=latexRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=waste, Unit="%",
                    TotalPrice=latexWaste},
                new PaintingBreakdownLine{ComponentName="Total Undercoat ", TotalPrice=latexTotal},

                new PaintingBreakdownLine{ ComponentName="Exterior Acrylic Latex.", Quantity=latexQty, Unit="Lit/m2",
                    UnitPrice= latexCost, TotalPrice=latexRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=waste, Unit="%",
                    TotalPrice=latexWaste},
                new PaintingBreakdownLine{ComponentName="Total FinishCoat ", TotalPrice=finishLatexTotal},


                new PaintingBreakdownLine{ ComponentName="Painter", Quantity=painterQty, Unit="per/day",
                    UnitPrice= painterCost, TotalPrice=painterRate},
                new PaintingBreakdownLine{ComponentName="Output", Quantity=painterOutput, Unit="m2/day", UnitPrice=painterRate,
                    TotalPrice=painLabour},

                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 8,
                Description = "Prepare and apply exterior acrylic latex as prime coat and finish coat on concrete surface.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }
        private PaintWorkItem ComputeItem9()
        {
            //Get value from steel work
            double paintRemovalLabour = 171; //GetSteelNetValue(_steelWorkViewModel.ComputeItem1);
            double paintRemovalQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Remove existing paint through power brushing. (see steel work)", 1);
            double paintRemovalRate = paintRemovalLabour * paintRemovalQty;

            //Primer Application
            double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
            double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double primerCost = GetMaterialPrice("Epoxy Mastic (Amerlock 400AL, Aluminium Grey)");
            double thinnerCost = GetMaterialPrice("Amercoat 9HF");

            double sprayingMachineQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Spraying machine", 0.10);
            double sprayingLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Labour spraying - spray painter", 0.15);
            double primerQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Aluminium epoxy primer", 0.15);
            double primerWastePer = UserRateEditStore.Current.Qty(SectionKey, 9, "Add waste", 15);
            double thinnerQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Thinner - Amercoat 9HF", primerQty * 0.1);

            double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
            double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
            double primerRate = primerCost * primerQty;
            double primerWaste = primerRate * (primerWastePer / 100);
            double thinnerRate = thinnerCost * thinnerQty;

            double totalPrimer = thinnerRate+ sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

            //Finish Application
            double finishMachineCost = (GetLabourRate("Spraying machine") / 8);
            double finishLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double polyurethanerCost = GetMaterialPrice("Ditto but Blue 5010 (Finish Coating)");
            double FinishthinnerCost = GetMaterialPrice("Amercoat 920");

            double finishMachineQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Spraying machine", 0.10);
            double finishLabouurQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Labour spraying - spray painter", 0.15);
            double polyurethanerQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Blue/White Polyurethane", 0.09);
            double polyurethanerWastePer = UserRateEditStore.Current.Qty(SectionKey, 9, "Add waste", 15);
            double thinnerFinishQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Thinner - Amercoat 920", polyurethanerQty * 0.1);

            double finishMachineRate = finishMachineCost * finishMachineQty;
            double finishLabourRate = finishLabourCost * finishLabouurQty;
            double polyurethanerRate = polyurethanerCost * polyurethanerQty;
            double polyurethanerWaste = polyurethanerRate * (polyurethanerWastePer / 100);
            double thinnerFinishRate = FinishthinnerCost * thinnerFinishQty;

            double totalFinish = finishMachineRate + finishLabourRate + polyurethanerRate + polyurethanerWaste + thinnerFinishRate;

            double netCostPerSqm = paintRemovalRate + totalPrimer + totalFinish;
            var ohp = ApplyOHP(netCostPerSqm);

            var breakdown = new ObservableCollection<PaintingBreakdownLine>
            {
                new PaintingBreakdownLine{ ComponentName="Remove existing paint through power brushing. (see steel work)", Quantity=paintRemovalQty,
                    Unit="m2",
                    UnitPrice= paintRemovalLabour, TotalPrice=paintRemovalRate},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=sprayingMachineQty, Unit="hr/m2",
                    UnitPrice= sprayingMachineCost, TotalPrice=sprayingMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=sprayingLabouurQty, Unit="hr/m2",
                    UnitPrice= sprayingLabourCost, TotalPrice=sprayingLabourRate},
                new PaintingBreakdownLine{ ComponentName="Aluminium epoxy primer", Quantity=primerQty, Unit="Lit/m2",
                    UnitPrice= primerCost, TotalPrice=primerRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=primerWastePer, Unit="%",
                    TotalPrice=primerWaste},
                new PaintingBreakdownLine{ ComponentName="Thinner - Amercoat 9HF", Quantity=thinnerQty, Unit="Lit/m2",
                    UnitPrice= thinnerCost, TotalPrice=thinnerRate},
                new PaintingBreakdownLine{ComponentName="Total Undercoat ", TotalPrice=totalPrimer},

                new PaintingBreakdownLine{ ComponentName="Spraying machine", Quantity=finishMachineQty, Unit="hr/m2",
                    UnitPrice= finishMachineCost, TotalPrice=finishMachineRate},
                new PaintingBreakdownLine{ ComponentName="Labour spraying - spray painter", Quantity=finishLabouurQty, Unit="hr/m2",
                    UnitPrice= finishLabourCost, TotalPrice=finishLabourRate},
                new PaintingBreakdownLine{ ComponentName="Blue/White Polyurethane", Quantity=polyurethanerQty, Unit="Lit/m2",
                    UnitPrice= polyurethanerCost, TotalPrice=polyurethanerRate},
                new PaintingBreakdownLine{ComponentName="Add waste", Quantity=polyurethanerWastePer, Unit="%",
                    TotalPrice=polyurethanerWaste},
                new PaintingBreakdownLine{ ComponentName="Thinner - Amercoat 920", Quantity=thinnerFinishQty, Unit="Lit/m2",
                    UnitPrice= FinishthinnerCost, TotalPrice=thinnerFinishRate},
                new PaintingBreakdownLine{ComponentName="Total Finish Coat ", TotalPrice=totalFinish},

                new PaintingBreakdownLine{ComponentName="Total Cost per m2", Unit="m2", TotalPrice=netCostPerSqm}
            };

            return new PaintWorkItem
            {
                ItemNo = 9,
                Description = "Remove existing paint through wire brushing, and prepare and apply aluminium epoxy primer as base and Mobil blue polyurethane as topcoat. (Ameron)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerSqm, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 2),
                PaintingBreakdownLines = breakdown
            };
        }

        #endregion
    }
}
