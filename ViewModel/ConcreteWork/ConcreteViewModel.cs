using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel.ConcreteWork
{
    public class ConcreteViewModel : ViewModelBase
    {
        private readonly GetItemsFromDB _helper;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        // Sorting / filtering helpers
        private bool _isNetCostFilterOn = false;          // toggled by “Filter ⌄”
        private SortState _currentSort = SortState.None;  // cycles in “Sort by ⌄”
        private enum SortState { None, Overhead, TotalCost }

        // ✅ Concrete section key
        private const string SectionKey = SectionKeys.Concrete;

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

        public ObservableCollection<ConcreteworkItem> ConcreteWorkItems { get; }
            = new ObservableCollection<ConcreteworkItem>();

        public ICollectionView ConcreteworkCollectionView { get; private set; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    RaisePropertyChanged();
                    ConcreteworkCollectionView?.Refresh();
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

        public ConcreteViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);

            // These can fire from non-UI threads depending on your service implementation
            matLib.LibraryChanged += OnLibraryChanged;
            labourLib.LibraryChanged += OnLibraryChanged;

            _computeEngine = new ComputeItemEngine(GetMaterialPrice, GetLabourRate);

            // ✅ Same pattern as GroundWork
            RateLibraryStore.Changed += OnLibraryChanged;

            // Load cached rate library first, then refresh from API (Concrete section)
            _ = LoadRateLibraryAsync();

            // Load compute catalog definitions (Concrete section)
            _ = LoadComputeCatalogForSectionAsync();

            BuildConcreteWorkItem();

            ConcreteworkCollectionView = CollectionViewSource.GetDefaultView(ConcreteWorkItems);
            ConcreteworkCollectionView.Filter = FilterConcreteWorkItem;

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

            // Rebuild items when the user-rate-edit store mutates (e.g. global Reset, per-item
            // Reset, cloud pull from QUIV/HERON). Iterate existing item *instances* in place so
            // the open popup's DataContext stays valid and re-renders via PropertyChanged.
            UserRateEditStore.Current.OverridesChanged += (_, __) =>
            {
                void Refresh()
                {
                    var nos = ConcreteWorkItems.Select(i => i.ItemNo).ToList();
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

                OnLibraryChanged(); // dispatcher-safe
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

                // Fallback: if API returns none for this section key, try without section filter
                if (ok && ComputeCatalogStore.LastApiItemCount == 0)
                {
                    await ComputeCatalogStore.RefreshFromApiAsync(); // fetch all
                }

                // Rebuild to append whatever is now in ComputeCatalogStore.Items
                RecomputeAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ComputeCatalog] load failed: {ex}");
            }
        }

        private void OnLibraryChanged()
        {
            // RateLibraryStore / ComputeCatalogStore can raise Changed from a background thread.
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

        private void RecomputeAll()
        {
            ConcreteWorkItems.Clear();
            BuildConcreteWorkItem();
            ConcreteworkCollectionView?.Refresh();
        }

        /// <summary>
        /// Re-runs the matching ComputeItemN method for the supplied ItemNo and updates the
        /// existing Item instance in place so the open popup keeps its DataContext binding live.
        /// Called by the breakdown popup after a user edits a Quantity.
        /// </summary>
        public void RecomputeItemInPlace(int itemNo)
        {
            var existing = ConcreteWorkItems.FirstOrDefault(i => i.ItemNo == itemNo);
            if (existing == null) return;

            Func<ConcreteworkItem>[] all =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16, ComputeItem17, ComputeItem18,
                ComputeItem19, ComputeItem20, ComputeItem21, ComputeItem22, ComputeItem23, ComputeItem24,
                ComputeItem25, ComputeItem26, ComputeItem27, ComputeItem28, ComputeItem29, ComputeItem30, ComputeItem31
            };

            ConcreteworkItem? fresh = null;
            foreach (var fn in all)
            {
                var candidate = fn();
                if (candidate.ItemNo == itemNo) { fresh = candidate; break; }
            }
            if (fresh == null) return;

            // Update parent-level properties in place — popup re-renders via PropertyChanged.
            existing.NetCost = fresh.NetCost;
            existing.OverheadValue = fresh.OverheadValue;
            existing.ProfitValue = fresh.ProfitValue;
            existing.TotalCost = fresh.TotalCost;

            // Replace breakdown line contents in place — keeps the ObservableCollection instance,
            // so the ItemsControl in the popup picks up the new rows automatically.
            existing.ConcreteBreakdownLine.Clear();
            foreach (var line in fresh.ConcreteBreakdownLine)
                existing.ConcreteBreakdownLine.Add(line);

            ConcreteworkCollectionView?.Refresh();
        }

        private void ShowDetails(object o)
        {
            if (o is ConcreteworkItem item)
            {
                var detailedControl = new ConcreteworkItemDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () => { SelectedDetail = null; };

                SelectedDetail = detailedControl;
            }
        }

        private bool FilterConcreteWorkItem(object obj)
        {
            if (obj is ConcreteworkItem item)
            {
                if (string.IsNullOrEmpty(SearchTerm)) return true;

                return item.Description?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            ConcreteworkCollectionView.SortDescriptions.Clear();

            if (_isNetCostFilterOn)
                ConcreteworkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(ConcreteworkItem.NetCost), ListSortDirection.Ascending));
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

            ConcreteworkCollectionView.SortDescriptions.Clear();

            switch (_currentSort)
            {
                case SortState.Overhead:
                    ConcreteworkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(ConcreteworkItem.OverheadValue), ListSortDirection.Ascending));
                    break;

                case SortState.TotalCost:
                    ConcreteworkCollectionView.SortDescriptions.Add(
                        new SortDescription(nameof(ConcreteworkItem.TotalCost), ListSortDirection.Ascending));
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

        private void BuildConcreteWorkItem()
        {
            Func<ConcreteworkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16, ComputeItem17, ComputeItem18,
                ComputeItem19, ComputeItem20, ComputeItem21, ComputeItem22, ComputeItem23, ComputeItem24,
                ComputeItem25, ComputeItem26, ComputeItem27, ComputeItem28, ComputeItem29, ComputeItem30, ComputeItem31
            };

            foreach (var compute in computeMethods)
                ConcreteWorkItems.Add(compute());

            // ✅ Append dynamic compute definitions (from API/disk store)
            AppendApiComputeItems();

            // ✅ Append admin rate items (from RateLibraryStore)
            AppendAdminRateItems();
        }

        private void AppendApiComputeItems()
        {
            if (_computeEngine == null) return;

            var defs = ComputeCatalogStore.Items;
            if (defs == null || defs.Count == 0)
            {
                Debug.WriteLine($"[ComputeCatalog] No items loaded for section '{SectionKey}'.");
                return;
            }

            int appended = 0;
            int nextNo = ConcreteWorkItems.Count + 1;

            foreach (var def in defs)
            {
                if (def == null || !def.enabled) continue;

                var key = SectionNormalizer.ToSectionKey(def.section);
                if (!string.Equals(key, SectionKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var computed = _computeEngine.Compute(def);

                    var net = (double)computed.NetCost;
                    var ohp = ApplyOHP(net);

                    var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>();

                    foreach (var l in computed.Lines)
                    {
                        breakdown.Add(new ConcreteworkBreakdownLine
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
                        breakdown.Add(new ConcreteworkBreakdownLine
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
                        breakdown.Add(new ConcreteworkBreakdownLine { ComponentName = "⚠ Warnings", TotalPrice = 0 });
                        foreach (var w in computed.Warnings)
                            breakdown.Add(new ConcreteworkBreakdownLine { ComponentName = $"- {w}", TotalPrice = 0 });
                    }

                    ConcreteWorkItems.Add(new ConcreteworkItem
                    {
                        ItemNo = nextNo++,
                        Description = def.name,
                        Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                        NetCost = Math.Round(net, 2),
                        OverheadValue = Math.Round(ohp.overheadVal, 0),
                        ProfitValue = Math.Round(ohp.profitVal, 0),
                        TotalCost = Math.Round(ohp.total, 2),
                        ConcreteBreakdownLine = breakdown
                    });

                    appended++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ComputeCatalog] Skip '{def?.name}': {ex.Message}");
                }
            }

            Debug.WriteLine($"[ComputeCatalog] Appended {appended} item(s) for section '{SectionKey}'.");
        }

        private void AppendAdminRateItems()
        {
            var rates = RateLibraryStore.Items;
            if (rates == null || rates.Count == 0)
            {
                Debug.WriteLine($"[RateLibrary] No rate items in store. File={ADLMRateGen.Services.RateLibraryStore.FilePath}");
                return;
            }

            int nextNo = ConcreteWorkItems.Count + 1;
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

                var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>();
                if (r.Breakdown != null)
                {
                    foreach (var l in r.Breakdown)
                    {
                        var lineTotal = (double)(l.LineTotal != 0 ? l.LineTotal : (l.Quantity * l.UnitPrice));

                        breakdown.Add(new ConcreteworkBreakdownLine
                        {
                            ComponentName = l.ComponentName,
                            Quantity = (double)l.Quantity,
                            Unit = l.Unit,
                            UnitPrice = (double)l.UnitPrice,
                            TotalPrice = lineTotal
                        });
                    }
                }

                ConcreteWorkItems.Add(new ConcreteworkItem
                {
                    ItemNo = nextNo++,
                    Description = r.Description,
                    Unit = string.IsNullOrWhiteSpace(r.Unit) ? "m2" : r.Unit,
                    NetCost = Math.Round(net, 2),
                    OverheadValue = Math.Round(ohp.overheadVal, 0),
                    ProfitValue = Math.Round(ohp.profitVal, 0),
                    TotalCost = Math.Round(ohp.total, 2),
                    ConcreteBreakdownLine = breakdown
                });

                appended++;
            }

            Debug.WriteLine($"[RateLibrary] Appended {appended} admin rate(s) for section '{SectionKey}'.");
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

        /// <summary>
        /// Returns the NetCost for a concrete item by ItemNo (e.g., 3, 4, 5...).
        /// Safe: returns 0 if not found.
        /// </summary>
        public double GetConcreteNetValue(int itemNo)
        {
            double result = 0;

            void Work()
            {
                var snapshot = ConcreteWorkItems?.ToList();
                var item = snapshot?.FirstOrDefault(i => i != null && i.ItemNo == itemNo);
                result = item?.NetCost ?? 0d;
            }

            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess()) disp.Invoke((Action)Work);
            else Work();

            if (result > 0) return result;

            // Fallback: try admin rate store (in case ConcreteWorkItems isn't loaded yet)
            var rates = RateLibraryStore.Items?.ToList();
            var r = rates?.FirstOrDefault(x =>
                x != null &&
                string.Equals(x.SectionKey ?? "", SectionKey, StringComparison.OrdinalIgnoreCase) &&
                x.NetCost > 0);

            return r != null ? (double)r.NetCost : 0d;
        }

        /// <summary>
        /// Returns the NetCost for a concrete item by matching description text (case-insensitive).
        /// Optionally filter by unit.
        /// Safe: returns 0 if not found.
        /// </summary>
        public double GetConcreteNetValue(string descriptionContains, string unit = null)
        {
            if (string.IsNullOrWhiteSpace(descriptionContains)) return 0d;

            double result = 0;

            void Work()
            {
                var snapshot = ConcreteWorkItems?.ToList();
                var item = snapshot?.FirstOrDefault(i =>
                    i != null &&
                    !string.IsNullOrWhiteSpace(i.Description) &&
                    i.Description.IndexOf(descriptionContains, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (string.IsNullOrWhiteSpace(unit) ||
                     string.Equals(i.Unit ?? "", unit, StringComparison.OrdinalIgnoreCase)));

                result = item?.NetCost ?? 0d;
            }

            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess()) disp.Invoke((Action)Work);
            else Work();

            if (result > 0) return result;

            // Fallback: admin rate store (works even if Concrete tab hasn't been opened)
            var rates = RateLibraryStore.Items?.ToList();
            var r = rates?.FirstOrDefault(x =>
                x != null &&
                string.Equals(x.SectionKey ?? "", SectionKey, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Description) &&
                x.Description.IndexOf(descriptionContains, StringComparison.OrdinalIgnoreCase) >= 0 &&
                (string.IsNullOrWhiteSpace(unit) ||
                 string.Equals(x.Unit ?? "", unit, StringComparison.OrdinalIgnoreCase)) &&
                x.NetCost > 0);

            return r != null ? (double)r.NetCost : 0d;
        }


        #region ComputeItem Method
        private ConcreteworkItem ComputeItem1()
        {
            // All user-editable quantities are declared as locals via Qty() so a single override
            // flows through every downstream formula (breakdown TotalPrice, sub-totals, NetCost).
            double mixerQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Concrete 10/14 mixer.", 1);
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double mixerLineTotal = mixerCost * mixerQty;

            double dieselPrice = GetLabourRate("Labourer") / 8;
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 1, "Fuel (Diesel)", 30);
            double fuelCost = dieselPrice * literPerDay;

            double oilPct = UserRateEditStore.Current.Qty(SectionKey, 1, "Oil and consumables (per day)", 3);
            double oilCost = (oilPct / 100.0) * fuelCost;

            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 1, "Operator (per day)", 2);
            double operatorRate = GetLabourRate("Heavy plant operator") * 1.4;
            double operatorTotal = operatorRate * operatorQty;

            double totalPlantDay = mixerLineTotal + fuelCost + oilCost + operatorTotal;

            double workHr = UserRateEditStore.Current.Qty(SectionKey, 1, "Cost per hour (8 hour Working Day)", 8);
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double netCostPerm3 = costPerHr / volPerHr;
            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
                new ConcreteworkBreakdownLine { ComponentName="Concrete 10/14 mixer.", Quantity=mixerQty, Unit="N/day", UnitPrice=mixerCost, TotalPrice=mixerLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=literPerDay, Unit="hr/m3", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new ConcreteworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=oilPct, Unit="%", UnitPrice=0, TotalPrice=oilCost },
                new ConcreteworkBreakdownLine { ComponentName="Operator (per day)", Quantity=operatorQty, Unit="Nr/Day", UnitPrice=operatorRate, TotalPrice=operatorTotal },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Cost per day", Quantity=1, Unit="", TotalPrice=totalPlantDay },

                new ConcreteworkBreakdownLine { ComponentName="Cost per hour (8 hour Working Day)", Quantity=workHr, Unit="N/Hr", UnitPrice=totalPlantDay, TotalPrice=costPerHr },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=volPerHr, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 1,
                Description = "Calculating plant and labour cost for mixing concrete," +
                " using 21/14 mixer. Note: mixer is running a 3 minute circle prior " +
                "to pouring out mix.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        public ConcreteworkItem ComputeItem2()
        {
            double mixer2114Qty = UserRateEditStore.Current.Qty(SectionKey, 2, "Concrete mixer 21/14", 1);
            double oilConsumQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Oil and consumables (per day)", 1);
            double operatorQty = UserRateEditStore.Current.Qty(SectionKey, 2, "Operator (per day)", 2);

            double mixerCost = GetLabourRate("Concrete mixer 21/14");
            double dieselPrice = (GetLabourRate("Labourer") / 8) *1.4;
            double literPerDay = UserRateEditStore.Current.Qty(SectionKey, 2, "Fuel (Diesel)", 40);
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.2;

            double totalPlantDay = (mixer2114Qty * mixerCost) + fuelCost +
                (oilConsumQty * 0.03 * fuelCost) + (operatorQty * operatorCost);

            double workHr = UserRateEditStore.Current.Qty(SectionKey, 2, "Cost per hour (8 hour Working Day)", 8);
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 7.94;
            double netCostPerm3 = costPerHr / volPerHr;
            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
                new ConcreteworkBreakdownLine { ComponentName="Concrete mixer 21/14", Quantity=mixer2114Qty, Unit="N/day", UnitPrice=mixerCost, TotalPrice=mixer2114Qty * mixerCost },
                new ConcreteworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=literPerDay, Unit="hr/m3", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new ConcreteworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=oilConsumQty, Unit="3%", TotalPrice=oilConsumQty * 0.03 * fuelCost },
                new ConcreteworkBreakdownLine { ComponentName="Operator (per day)", Quantity=operatorQty, Unit="Nr/Day", UnitPrice=operatorCost, TotalPrice=operatorCost*operatorQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Cost per day", Quantity=1, Unit="", TotalPrice=totalPlantDay },

                new ConcreteworkBreakdownLine { ComponentName="Cost per hour (8 hour Working Day)", Quantity=workHr, Unit="N/Hr", UnitPrice=totalPlantDay, TotalPrice=costPerHr },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=volPerHr, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 2,
                Description = "Calculating plant and labour cost for mixing concrete, using 21/14 mixer. Note mixer is running a 3 minute circle prior to pouring out mix.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        public ConcreteworkItem ComputeItem3()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 3, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            //double stonePrice = GetMaterialPrice("Washed gravel (local)") ;
            double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling")* 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 3, "Cement", 3.32);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 3, "Sand", 0.46);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 3, "Granite (including transportation)", 0.90);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 3, "Mixing crew - labour.", 6);
            double mixingCrewCost = ((GetLabourRate("Labourer")));
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 3, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)");
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 3, "Placing crew - labour.", 6);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 3, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 3, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost+totalPlacingCrewCost+totalMasonCrewCost+totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 3,
                Description = "Concrete (1:4:8) grade 10 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem4()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 4, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 4, "Cement", 4.32);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 4, "Sand", 0.45);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 4, "Granite (including transportation)", 0.90);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 4, "Mixing crew - labour.", 6);
            double mixingCrewCost = ((GetLabourRate("Labourer")/8)*1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 4, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)")/8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 4, "Placing crew - labour.", 6);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 4, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 4, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 4,
                Description = "Concrete (1:3:6) grade 15 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem5()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 5, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 5, "Cement", 6.16);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 5, "Sand", 0.43);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 5, "Granite (including transportation)", 0.86);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 5, "Mixing crew - labour.", 6);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 5, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 5, "Placing crew - labour.", 6);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 5, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 5, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 5,
                Description = "Concrete (1:2:4) grade 20 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem6()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 6, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 6, "Cement", 7.86);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 6, "Sand", 0.41);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 6, "Granite (including transportation)", 0.82);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 6, "Mixing crew - labour.", 6);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 6, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 6, "Placing crew - labour.", 6);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 6, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 6, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 6,
                Description = "Concrete (1:1.5:3) grade 25 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem7()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 7, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 7, "Cement", 6.16);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 7, "Sand", 0.43);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 7, "Granite (including transportation)", 0.86);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 7, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 7, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 7, "Placing crew - labour.", 12);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 7, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 7, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 7,
                Description = "Concrete (1:2:4) grade 20 in column, wall or suspended floor.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem8()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 8, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 8, "Cement", 7.86);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 8, "Sand", 0.41);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 8, "Granite (including transportation)", 0.82);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 8, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 8, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 8, "Placing crew - labour.", 12);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 8, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 8, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 8,
                Description = "Concrete (1:1.5:3) grade 25 in column, wall or suspended floor.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem9()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 9, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 9, "Cement", 18);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 9, "Sand", 0.3);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 9, "Granite (including transportation)", 0.6);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 9, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 9, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 9, "Placing crew - labour.", 9);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 9, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 9, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 9,
                Description = "Concrete (1:1/2:1) grade 35 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem10()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 10, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 10, "Cement", 4.2);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 10, "Sand", 0.6);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 10, "Granite (including transportation)", 0.74);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 10, "Mixing crew - labour.", 4);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 10, "Poker vibrator", 1.25);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 10, "Placing crew - labour.", 4);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 10, "Mason.", 1);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 10, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 10,
                Description = "Concrete (1:4:5) grade 35 in suspended slab or wall.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem11()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Mild Steel: 6mm steel reinforcement (350 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 11, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/4\" diameter (350 pieces) - 6mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll")/25;
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 11, "Binding wire", 10);
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 11, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 11, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel+ concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 11, "Steelfixer hours", 48);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 11, "Labour hours", 48);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 5.4;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 24;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Mild Steel: 6mm steel reinforcement (350 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 11,
                Description = "Procure and place 6mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem12()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Mild Steel: 10-12mm steel reinforcement (180 - 112 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 12, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (112 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 12, "Binding wire", 10);
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 12, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 12, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 12, "Steelfixer hours", 44);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 12, "Labour hours", 44);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 4.8;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 22;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Mild Steel: 10-12mm steel reinforcement (180 - 112 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 12,
                Description = "Procure and place 10 to 12mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem13()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 13, "High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 13, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 13, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 13, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 13, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 13, "Steelfixer hours", 48);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 13, "Labour hours", 33);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 4.8;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 16.5;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 13,
                Description = "Procure and place 10 to 12mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem14()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 14, "High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 14, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 14, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 14, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 14, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 14, "Steelfixer hours", 33);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 14, "Labour hours", 33);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 6.3;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 16.5;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 14,
                Description = "Procure and place 7 to 12mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem15()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 15, "High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 15, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 15, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 15, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 15, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 15, "Steelfixer hours", 48);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 15, "Labour hours", 33);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 6.3;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 16.5;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 10 - 12mm steel reinforcement (133 - 93 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 15,
                Description = "Procure and place 7 to 12mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem16()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 16, "High Tensile Steel: 16 - 18mm steel reinforcement (52 - 33 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 16, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("5/8\" diameter (52 pieces) - 16mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 16, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 16, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 16, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 16, "Steelfixer hours", 24);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 16, "Labour hours", 24);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 4.2;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 12;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 16 - 18mm steel reinforcement (52 - 33 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 16,
                Description = "Procure and place 16 to 18mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 3.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem17()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 17, "High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 17, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 17, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 17, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 17, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 17, "Steelfixer hours", 39);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 17, "Labour hours", 39);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 4.2;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 19.5;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 17,
                Description = "Procure and place 12 to 18mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not not exceeding 3.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem18()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 18, "High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 18, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 18, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 18, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 18, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 18, "Steelfixer hours", 24);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 18, "Labour hours", 24);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 5.4;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 12;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 18,
                Description = "Procure and place 13 to 19mm plain round reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem19()
        {
            double rebarQty = UserRateEditStore.Current.Qty(SectionKey, 19, "High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", 1);
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 19, "Add for waste.", 1);

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double rebarLineTotal = rebarPrice * rebarQty;
            double wastePer = 10;
            double rebarWaste = wasteQty * rebarPrice * (wastePer / 100);
            double bindingQtyPerTon = UserRateEditStore.Current.Qty(SectionKey, 19, "Binding wire", 10);
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 19, "Unloading steel. - 2 labour", 3);
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = UserRateEditStore.Current.Qty(SectionKey, 19, "Concrete spacers", 5);
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalMaterialCost = rebarLineTotal + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalMaterialCost = totalMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 19, "Steelfixer hours", 39);
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = UserRateEditStore.Current.Qty(SectionKey, 19, "Labour hours", 39);
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 5.4;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 19.5;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="High Tensile Steel: 12 - 18mm steel reinforcement (93 - 33 pieces)", Quantity=rebarQty, Unit="tonne", UnitPrice=rebarPrice, TotalPrice=rebarLineTotal },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="10%", TotalPrice=rebarWaste },
                new ConcreteworkBreakdownLine { ComponentName="Binding wire", Quantity=bindingQtyPerTon, Unit="kg/tonne", UnitPrice=bindingWirePrice, TotalPrice=totalBindingWire },
                new ConcreteworkBreakdownLine { ComponentName="Unloading steel. - 2 labour", Quantity=unloadingDurationPerTon, Unit="hr/tonne", UnitPrice=unloadingSteelLabour, TotalPrice=totalUnloadingSteel},
                new ConcreteworkBreakdownLine { ComponentName="Concrete spacers", Quantity=concreteSpacerPer, Unit="% of Steel", TotalPrice=concreteSpacerQty },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steelfixer hours", Quantity=steelFixingDurationPerTon, Unit="hr/tonne", UnitPrice=steelFixingLabourCost, TotalPrice=totalSteelFixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerTon, Unit="hr/tonne", UnitPrice=steelLabourCost, TotalPrice=totalSteelLabourCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Cutting and Fixing Per tonne", Quantity=1, Unit="", TotalPrice=totalSteelLabourFixingCost },

				//LABOUR HOISTING
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour Hoisting hours", Quantity=steelHoistingDurationPerTon, Unit="hr/tonne", UnitPrice=steelHoistingLabourCost, TotalPrice=totalSteelHoistingCost },

				//SUPERVISION LABOUR
				new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=steelHeadmanDurationPerTon, Unit="hr/tonne", UnitPrice=steelHeadmanLabourCost, TotalPrice=totalSteelHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="tonne", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 19,
                Description = "Procure and place 13 to 19mm deformed bar reinforcement in wall beams floor and roofs, hoisted in position at height not exceeding 6.00m",
                Unit = "tonne",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem20()
        {
            double cuttingWasteQty = UserRateEditStore.Current.Qty(SectionKey, 20, "Add cutting waste.", 6);

            //MATERIAL COST
            double boardLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 20, "25 x 300mm x 3600mm boards.", 0.93);
            double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
            double totalBoardPrice = boardPrice * boardLengthPerm2;
            double propsAndBraceLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 20, "50 x 75mm props and braces.", 1.56);
            double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
            double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;
            double propsAndBrace2LengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 20, "50 x 50mm, props and braces.", 2.25);
            double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
            double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
            double totalWoodPrice = totalBoardPrice+totalpropsAndBracePrice+ totalpropsAndBrace2Price;
            double cuttingWaste = totalWoodPrice * (cuttingWasteQty / 100);
            double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

            double nailFirstUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Allow for nails: first use", 0.5);
            double nailFiveUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Allow for nails for 5 subsequent  uses.", 0.125 * 5);
            double nailSixUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Nails for 6 uses.", nailFirstUse + nailFiveUse);
            double nailPrice = GetMaterialPrice("Nails 4\"")/25;
            double totalNailPrice = nailPrice * nailSixUse;

            double mouldOilSingleUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Allow for mould oil", 0.27);
            double mouldOilSixUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Mould oil for 6 uses", mouldOilSingleUse * 6);
            double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
            double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

            double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
            double materialCostPerUse = totalMaterialCost / 6;

            //LABOUR COST
            double carpenterDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Carpenter hours", 1.8);
            double carpenterDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Carpenter time for 5 repetitive use-times.", 1.3 * 5);
            double totalCarpentaDurationPer6Use = carpenterDurationPerFirstUse + carpenterDurationPerFiveUse;
            double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

            double labourDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Labour hours", 1.8);
            double labourDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 20, "Labour time for 5 repetitive use-times.", 1.3 * 5);
            double totalLabourDurationPer6Use = labourDurationPerFirstUse + labourDurationPerFiveUse;
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

            double headmanDurationPerSqm = 4.15;
            double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

            double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
            double totalLabourPerUse = totalLabourCostForSixUse / 6;

            double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

            double netCostPerTon = materialCostPerUse + totalLabourPerUse;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="25 x 300mm x 3600mm boards.", Quantity=boardLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
                new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=cuttingWasteQty, Unit="%",  TotalPrice=cuttingWaste},
                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

                new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



                new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
                new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 20,
                Description = "Sawn formwork to sides and soffit of beams and lintels not exceeding 3.00mm height from ground floor level. Six (6) number maximum uses.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem21()
        {
            double cuttingWasteQty = UserRateEditStore.Current.Qty(SectionKey, 21, "Add cutting waste.", 6);

            //MATERIAL COST
            double boardLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 21, "25 x 300mm x 3600mm boards.", 0.93);
            double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
            double totalBoardPrice = boardPrice * boardLengthPerm2;

            double propsAndBraceLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 21, "50 x 75mm props and braces.", 1.56);
            double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
            double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

            double propsAndBrace2LengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 21, "50 x 50mm, props and braces.", 2.25);
            double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
            double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
            double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

            double cuttingWaste = totalWoodPrice * (cuttingWasteQty / 100);
            double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

            double nailFirstUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Allow for nails: first use", 0.5);
            double nailFiveUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Allow for nails for 5 subsequent  uses.", 0.125 * 5);
            double nailSixUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Nails for 6 uses.", nailFirstUse + nailFiveUse);
            double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
            double totalNailPrice = nailPrice * nailSixUse;

            double mouldOilSingleUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Allow for mould oil", 0.27);
            double mouldOilSixUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Mould oil for 6 uses", mouldOilSingleUse * 6);
            double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
            double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

            double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
            double materialCostPerUse = totalMaterialCost / 6;

            //LABOUR COST
            double carpenterDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Carpenter hours", 2.07);
            double carpenterDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Carpenter time for 5 repetitive use-times.", 1.5 * 5);
            double totalCarpentaDurationPer6Use = carpenterDurationPerFirstUse + carpenterDurationPerFiveUse;
            double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

            double labourDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Labour hours", 2.07);
            double labourDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 21, "Labour time for 5 repetitive use-times.", 1.5 * 5);
            double totalLabourDurationPer6Use = labourDurationPerFirstUse + labourDurationPerFiveUse;
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

            double headmanDurationPerSqm = 4.77;
            double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

            double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
            double totalLabourPerUse = totalLabourCostForSixUse / 6;

            double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

            double netCostPerTon = materialCostPerUse + totalLabourPerUse;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="25 x 300mm x 3600mm boards.", Quantity=boardLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
                new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=cuttingWasteQty, Unit="%",  TotalPrice=cuttingWaste},
                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

                new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



                new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
                new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 21,
                Description = "Sawn formwork to sides and soffit of beams and lintels over 3.00mm but not exceeding 6.00m height from ground floor level. Six (6) number maximum uses.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem22()
        {
            double cuttingWasteQty = UserRateEditStore.Current.Qty(SectionKey, 22, "Add cutting waste.", 6);

            //MATERIAL COST
            double boardLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 22, "25 x 300mm x 3600mm boards.", 0.93);
            double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
            double totalBoardPrice = boardPrice * boardLengthPerm2;

            double propsAndBraceLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 22, "50 x 75mm props and braces.", 1.56);
            double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
            double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

            double propsAndBrace2LengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 22, "50 x 50mm, props and braces.", 2.25);
            double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
            double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
            double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

            double cuttingWaste = totalWoodPrice * (cuttingWasteQty / 100);
            double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

            double nailFirstUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Allow for nails: first use", 0.5);
            double nailFiveUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Allow for nails for 5 subsequent  uses.", 0.125 * 5);
            double nailSixUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Nails for 6 uses.", nailFirstUse + nailFiveUse);
            double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
            double totalNailPrice = nailPrice * nailSixUse;

            double mouldOilSingleUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Allow for mould oil", 0.27);
            double mouldOilSixUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Mould oil for 6 uses", mouldOilSingleUse * 6);
            double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
            double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

            double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
            double materialCostPerUse = totalMaterialCost / 6;

            //LABOUR COST
            double carpenterDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Carpenter hours", 1.62);
            double carpenterDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Carpenter time for 5 repetitive use-times.", 1.13 * 5);
            double totalCarpentaDurationPer6Use = Math.Round(carpenterDurationPerFirstUse + carpenterDurationPerFiveUse, 2);
            double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

            double labourDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Labour hours", 1.62);
            double labourDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 22, "Labour time for 5 repetitive use-times.", 1.13 * 5);
            double totalLabourDurationPer6Use = Math.Round(labourDurationPerFirstUse + labourDurationPerFiveUse, 2);
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

            double headmanDurationPerSqm = 4.19;
            double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

            double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
            double totalLabourPerUse = totalLabourCostForSixUse / 6;

            double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

            double netCostPerTon = materialCostPerUse + totalLabourPerUse;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="25 x 300mm x 3600mm boards.", Quantity=boardLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
                new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=cuttingWasteQty, Unit="%",  TotalPrice=cuttingWaste},
                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

                new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



                new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
                new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 22,
                Description = "Sawn formwork to vertical sides of columns not exceeding 3.00mm height from ground floor level. Six (6) number maximum uses.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem23()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 23, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 23, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 23, "Cement", 10.8);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 23, "Sand", 0.38);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 23, "Granite (including transportation)", 0.75);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 23, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 23, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 23, "Placing crew - labour.", 9);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 23, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 23, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 23,
                Description = "Concrete (1:1:2) grade 30 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem24()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 24, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 24, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 24, "Cement", 10.8);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 24, "Sand", 0.38);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 24, "Granite (including transportation)", 0.75);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 24, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 24, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 24, "Placing crew - labour.", 12);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 24, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 24, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 24,
                Description = "Concrete (1:1:2) grade 30 in suspended slab or wall.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem25()
        {
            double meshPerM2Qty = UserRateEditStore.Current.Qty(SectionKey, 25, "Mesh Cost Per M2", 1);

            //MATERIAL COST
            double meshPrice = GetMaterialPrice("A142 at 2.22kg/m2 size 4.8 x 2.4m x 2.2kg/m2");
            double sheetSize = UserRateEditStore.Current.Qty(SectionKey, 25, "Sheet size 2.4m x 4.8m wide.", 11.52);
            double costPerSqm = meshPerM2Qty * (meshPrice/sheetSize);
            double bindingWirePer = UserRateEditStore.Current.Qty(SectionKey, 25, "Add  for binding wire and overlaps", 5);
            double bindingWireCost = costPerSqm * (bindingWirePer / 100);
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 25, "Add for waste.", 5);
            double wasteCost = (costPerSqm+ bindingWireCost) * (wastePer / 100);

            double totalMaterial = costPerSqm + bindingWireCost + wasteCost;

            //LABOUR COST
            double steelFixingLabour = UserRateEditStore.Current.Qty(SectionKey, 25, "Steel Fixers", 0.15);
            double steelFixerCostPerHr = ((GetLabourRate("Welder") / 8) * 1.4);
            double totalSteelFixerCost = steelFixingLabour * steelFixerCostPerHr;

            double labourDuration = UserRateEditStore.Current.Qty(SectionKey, 25, "Labour", 0.15);
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totallabourCost = labourDuration * labourCostPerHr;

            double totalMeshLabour = totalSteelFixerCost+totallabourCost;

            double totalcostPerM2 = totalMaterial + totalMeshLabour;

            double netCostPerm2 = totalcostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Material cost", Quantity=UserRateEditStore.Current.Qty(SectionKey, 25, "Material cost", 1), Unit="", UnitPrice=meshPrice, },
                new ConcreteworkBreakdownLine { ComponentName="Sheet size 2.4m x 4.8m wide.", Quantity=sheetSize, Unit="m2" },
                new ConcreteworkBreakdownLine { ComponentName="Mesh Cost Per M2", Quantity=meshPerM2Qty, Unit="N/m2", TotalPrice=costPerSqm },
                new ConcreteworkBreakdownLine { ComponentName="Add  for binding wire and overlaps", Quantity=bindingWirePer, Unit="%",  TotalPrice=bindingWireCost},
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%",  TotalPrice=wasteCost},
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterial },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steel Fixers", Quantity=steelFixingLabour, Unit="hr/m2", UnitPrice=steelFixerCostPerHr, TotalPrice=totalSteelFixerCost},
                new ConcreteworkBreakdownLine { ComponentName="Labour", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totallabourCost},

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour", Quantity=1, Unit="m2", TotalPrice=totalMeshLabour },
                new ConcreteworkBreakdownLine { ComponentName="Total: ", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 25,
                Description = "Procure and place BRC mesh Ref. A142 in floor slab",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem26()
        {
            double meshPerM2Qty = UserRateEditStore.Current.Qty(SectionKey, 26, "Mesh Cost Per M2", 1);

            //MATERIAL COST
            double meshPrice = GetMaterialPrice("A193 at 3.95kg/m2 size 4.8 x 2.4m x 3.02kg/m2");
            double sheetSize = UserRateEditStore.Current.Qty(SectionKey, 26, "Sheet size 2.4m x 4.8m wide.", 11.52);
            double costPerSqm = meshPerM2Qty * (meshPrice / sheetSize);
            double bindingWirePer = UserRateEditStore.Current.Qty(SectionKey, 26, "Add  for binding wire and overlaps", 5);
            double bindingWireCost = costPerSqm * (bindingWirePer / 100);
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 26, "Add for waste.", 5);
            double wasteCost = (costPerSqm + bindingWireCost) * (wastePer / 100);

            double totalMaterial = costPerSqm + bindingWireCost + wasteCost;

            //LABOUR COST
            double steelFixingLabour = UserRateEditStore.Current.Qty(SectionKey, 26, "Steel Fixers", 0.18);
            double steelFixerCostPerHr = ((GetLabourRate("Welder") / 8) * 1.4);
            double totalSteelFixerCost = steelFixingLabour * steelFixerCostPerHr;

            double labourDuration = UserRateEditStore.Current.Qty(SectionKey, 26, "Labour", 0.18);
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totallabourCost = labourDuration * labourCostPerHr;

            double totalMeshLabour = totalSteelFixerCost + totallabourCost;

            double totalcostPerM2 = totalMaterial + totalMeshLabour;

            double netCostPerm2 = totalcostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Material cost", Quantity=UserRateEditStore.Current.Qty(SectionKey, 26, "Material cost", 1), Unit="", UnitPrice=meshPrice, },
                new ConcreteworkBreakdownLine { ComponentName="Sheet size 2.4m x 4.8m wide.", Quantity=sheetSize, Unit="m2" },
                new ConcreteworkBreakdownLine { ComponentName="Mesh Cost Per M2", Quantity=meshPerM2Qty, Unit="N/m2", TotalPrice=costPerSqm },
                new ConcreteworkBreakdownLine { ComponentName="Add  for binding wire and overlaps", Quantity=bindingWirePer, Unit="%",  TotalPrice=bindingWireCost},
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%",  TotalPrice=wasteCost},
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterial },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Steel Fixers", Quantity=steelFixingLabour, Unit="hr/m2", UnitPrice=steelFixerCostPerHr, TotalPrice=totalSteelFixerCost},
                new ConcreteworkBreakdownLine { ComponentName="Labour", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totallabourCost},

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour", Quantity=1, Unit="m2", TotalPrice=totalMeshLabour },
                new ConcreteworkBreakdownLine { ComponentName="Total: ", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 26,
                Description = "Procure and place BRC mesh Ref. A193 in floor slab",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem27()
        {
            double concreteGradeQty = UserRateEditStore.Current.Qty(SectionKey, 27, "Concrete grade 30", 1);
            double materialCostQty = UserRateEditStore.Current.Qty(SectionKey, 27, "Material cost", 1);
            double mixingPrecastQty = UserRateEditStore.Current.Qty(SectionKey, 27, "Mixing - as before calculated", 1);
            double placingPrecastQty = UserRateEditStore.Current.Qty(SectionKey, 27, "Placing - as before calculated", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = 10.8;
            double sandPerM3 = 0.38;
            double stonePerM3 = 0.75;
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = 9;
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = plantCostPerm3 + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = 0.24;
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = 12;
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = 2;
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = 1;
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            //MATERIAL COST
            double rebarPrice = GetMaterialPrice("1/2\" diameter (93 pieces) - 12mm diameter.");
            double wasteRebarPer = 10;
            double rebarWaste = rebarPrice * (wasteRebarPer / 100);
            double bindingQtyPerTon = 10;
            double bindingWirePrice = GetMaterialPrice("Binding Wire - 25kg roll") / 25;
            double totalBindingWire = bindingWirePrice * bindingQtyPerTon;
            double unloadingSteelLabour = (GetLabourRate("Labourer") / 8) * 1.4 * 2;
            double unloadingDurationPerTon = 3;
            double totalUnloadingSteel = unloadingSteelLabour * unloadingDurationPerTon;
            double concreteSpacerPer = 5;
            double concreteSpacerQty = rebarPrice * (concreteSpacerPer / 100);

            double totalRebarMaterialCost = rebarPrice + rebarWaste + totalBindingWire + totalUnloadingSteel + concreteSpacerQty;
            double finalRebarMaterialCost = totalRebarMaterialCost;

            //LABOUR COST
            double steelFixingDurationPerTon = 39;
            double steelFixingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double totalSteelFixingCost = steelFixingDurationPerTon * steelFixingLabourCost;

            double labourDurationPerTon = 39;
            double steelLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelLabourCost = labourDurationPerTon * steelLabourCost;

            double totalSteelLabourFixingCost = totalSteelFixingCost + totalSteelLabourCost;

            double steelHoistingDurationPerTon = 4.2;
            double steelHoistingLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double totalSteelHoistingCost = steelHoistingDurationPerTon * steelHoistingLabourCost;

            double steelHeadmanDurationPerTon = 19.5;
            double steelHeadmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalSteelHeadmanCost = steelHeadmanDurationPerTon * steelHeadmanLabourCost;

            double netCostPerTon = finalRebarMaterialCost + totalSteelLabourFixingCost + totalSteelHoistingCost + totalSteelHeadmanCost;

            //MATERIAL COST
            double boardLengthPerm2 = 0.93;
            double boardPrice = GetMaterialPrice("1x12\"x12' (25x300x3600mm)");
            double totalBoardPrice = boardPrice * boardLengthPerm2;

            double propsAndBraceLengthPerm2 = 1.56;
            double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
            double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

            double propsAndBrace2LengthPerm2 = 2.25;
            double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
            double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
            double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

            double wastePerM2 = 5;
            double cuttingWaste = totalWoodPrice * (wastePerM2 / 100);
            double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

            double nailFirstUse = 0.5;
            double nailFiveUse = 0.125 * 5;
            double nailSixUse = nailFirstUse + nailFiveUse;
            double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
            double totalNailPrice = nailPrice * nailSixUse;

            double mouldOilSingleUse = 0.27;
            double mouldOilSixUse = mouldOilSingleUse * 6;
            double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
            double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

            double totalWoodMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
            double materialCostPerUse = totalWoodMaterialCost / 6;

            //LABOUR COST
            double carpenterDurationPerFirstUse = 1.62;
            double carpenterDurationPerFiveUse = 1.13 * 5;
            double totalCarpentaDurationPer6Use = Math.Round(carpenterDurationPerFirstUse + carpenterDurationPerFiveUse, 2);
            double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

            double labourDurationPerFirstUse = 1.62;
            double labourDurationPerFiveUse = 1.13 * 5;
            double totalLabourDurationPer6Use = Math.Round(labourDurationPerFirstUse + labourDurationPerFiveUse, 2);
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

            double headmanDurationPerSqm = 4.19;
            double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

            double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
            double totalLabourPerUse = totalLabourCostForSixUse / 6;

            double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

            double netWoodCostPerM2 = materialCostPerUse + totalLabourPerUse;

            //PRECAST CALCULATION
            double precastRebarQty = UserRateEditStore.Current.Qty(SectionKey, 27, "Reinforcement", 0.12);
            double precastRebarCost = precastRebarQty * netCostPerTon;

            double precastWoodQty = UserRateEditStore.Current.Qty(SectionKey, 27, "Formwork", 5);
            double precastWoodCost = precastWoodQty * netWoodCostPerM2;

            double precastTotal = (concreteGradeQty * netCostPerm3) + precastRebarCost + precastWoodCost;

            double mixingPreCastCrewduration = 9;
            double mixingPrecastCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPreCastMixingCost = mixingPreCastCrewduration * mixingPrecastCrewCost;

            double placingPercastCrewPerHr = 12;
            double placingPrecastCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingPrecastCrewCost = placingPercastCrewPerHr * placingPrecastCrewCost;

            double totalPrecastLabour = (mixingPrecastQty * totalPreCastMixingCost) + (placingPrecastQty * totalPlacingPrecastCrewCost);

            double precastNetTotal = precastTotal + totalPrecastLabour;

            double extraPreCastPer = UserRateEditStore.Current.Qty(SectionKey, 27, "Extra over costs for incidental works associated", 30);
            double extraPrecast = precastNetTotal * (extraPreCastPer / 100);
            double precastFinalTotal = precastNetTotal + extraPrecast;


            var ohp = ApplyOHP(precastFinalTotal);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Concrete grade 30", Quantity=concreteGradeQty, Unit="m3", UnitPrice=netCostPerm3, TotalPrice=concreteGradeQty * netCostPerm3},
                new ConcreteworkBreakdownLine { ComponentName="Reinforcement", Quantity=precastRebarQty, Unit="Tonne/m3", UnitPrice=netCostPerTon, TotalPrice=precastRebarCost},
                new ConcreteworkBreakdownLine { ComponentName="Formwork", Quantity=precastWoodQty, Unit="m2/m3", UnitPrice=netWoodCostPerM2, TotalPrice=precastWoodCost},
                new ConcreteworkBreakdownLine { ComponentName="Material cost", Quantity=materialCostQty, Unit="", TotalPrice=precastTotal },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Mixing - as before calculated", Quantity=mixingPrecastQty, Unit="lot/m3", UnitPrice=totalPreCastMixingCost, TotalPrice=mixingPrecastQty * totalPreCastMixingCost},
                new ConcreteworkBreakdownLine { ComponentName="Placing - as before calculated", Quantity=placingPrecastQty, Unit="lot/m3", UnitPrice=totalPlacingPrecastCrewCost, TotalPrice=placingPrecastQty * totalPlacingPrecastCrewCost},

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Labour", Quantity=1, Unit="lot/m3", TotalPrice=totalPrecastLabour },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material & Labour", Quantity=1, Unit="m3", TotalPrice=precastNetTotal },
                new ConcreteworkBreakdownLine { ComponentName="Extra over costs for incidental works associated", Quantity=extraPreCastPer, Unit="%", TotalPrice=extraPrecast},

                new ConcreteworkBreakdownLine { ComponentName="Total: Cost per m3 ", Quantity=1, Unit="m3", TotalPrice=precastFinalTotal },
            };

            return new ConcreteworkItem
            {
                ItemNo = 27,
                Description = "Precast concrete grade 30 Material (1m3 Of Concrete)",
                Unit = "m2",
                NetCost = Math.Round(precastFinalTotal, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem28()
        {
            double cuttingWasteQty = UserRateEditStore.Current.Qty(SectionKey, 28, "Add cutting waste.", 6);

            //MATERIAL COST
            double plyWoodLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 28, "18mm plywood", 0.35);
            double boardPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
            double totalBoardPrice = boardPrice * plyWoodLengthPerm2;

            double propsAndBraceLengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 28, "50 x 75mm props and braces.", 1.56);
            double propsAndBracePrice = GetMaterialPrice("2x3\"x12' (50x75x3600mm)");
            double totalpropsAndBracePrice = propsAndBraceLengthPerm2 * propsAndBracePrice;

            double propsAndBrace2LengthPerm2 = UserRateEditStore.Current.Qty(SectionKey, 28, "50 x 50mm, props and braces.", 2.25);
            double propsAndBrace2Price = GetMaterialPrice("2x2\"x12' (50x50x3600mm)");
            double totalpropsAndBrace2Price = propsAndBrace2LengthPerm2 * propsAndBrace2Price;
            double totalWoodPrice = totalBoardPrice + totalpropsAndBracePrice + totalpropsAndBrace2Price;

            double cuttingWaste = totalWoodPrice * (cuttingWasteQty / 100);
            double subTotalWoodPrice = totalWoodPrice + cuttingWaste;

            double nailFirstUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Allow for nails: first use", 0.5);
            double nailFiveUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Allow for nails for 5 subsequent  uses.", 0.125 * 5);
            double nailSixUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Nails for 6 uses.", nailFirstUse + nailFiveUse);
            double nailPrice = GetMaterialPrice("Nails 4\"") / 25;
            double totalNailPrice = nailPrice * nailSixUse;

            double mouldOilSingleUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Allow for mould oil", 0.27);
            double mouldOilSixUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Mould oil for 6 uses", mouldOilSingleUse * 6);
            double mouldOilPrice = GetMaterialPrice("Mould oil") / 4;
            double totalMouldOilPrice = mouldOilPrice * mouldOilSixUse;

            double totalMaterialCost = subTotalWoodPrice + totalNailPrice + totalMouldOilPrice;
            double materialCostPerUse = totalMaterialCost / 6;

            //LABOUR COST
            double carpenterDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Carpenter hours", 1.62);
            double carpenterDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Carpenter time for 5 repetitive use-times.", 1.13 * 5);
            double totalCarpentaDurationPer6Use = Math.Round(carpenterDurationPerFirstUse + carpenterDurationPerFiveUse, 2);
            double carpenterCostPerHr = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalCarpenterCost = totalCarpentaDurationPer6Use * carpenterCostPerHr;

            double labourDurationPerFirstUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Labour hours", 1.62);
            double labourDurationPerFiveUse = UserRateEditStore.Current.Qty(SectionKey, 28, "Labour time for 5 repetitive use-times.", 1.13 * 5);
            double totalLabourDurationPer6Use = Math.Round(labourDurationPerFirstUse + labourDurationPerFiveUse, 2);
            double labourCostPerHr = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalLabourCost = totalLabourDurationPer6Use * labourCostPerHr;

            double headmanDurationPerSqm = 4.19;
            double headmanLabourCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCost = headmanDurationPerSqm * headmanLabourCost;

            double totalLabourCostForSixUse = totalCarpenterCost + totalLabourCost + totalHeadmanCost;
            double totalLabourPerUse = totalLabourCostForSixUse / 6;

            double totalSixUse = totalMaterialCost + totalLabourCostForSixUse;

            double netCostPerTon = materialCostPerUse + totalLabourPerUse;

            var ohp = ApplyOHP(netCostPerTon);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="18mm plywood", Quantity=plyWoodLengthPerm2, Unit="Length/m2", UnitPrice=boardPrice, TotalPrice=totalBoardPrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 75mm props and braces.", Quantity=propsAndBraceLengthPerm2, Unit="Length/m2",UnitPrice=propsAndBracePrice, TotalPrice=totalpropsAndBracePrice },
                new ConcreteworkBreakdownLine { ComponentName="50 x 50mm, props and braces.", Quantity=propsAndBrace2LengthPerm2, Unit="Length/m2", UnitPrice=propsAndBrace2Price, TotalPrice=totalpropsAndBrace2Price },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Wood Per Length", Quantity=1, Unit="", TotalPrice=totalWoodPrice },
                new ConcreteworkBreakdownLine { ComponentName="Add cutting waste.", Quantity=cuttingWasteQty, Unit="%",  TotalPrice=cuttingWaste},
                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=subTotalWoodPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for nails: first use", Quantity=nailFirstUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Allow for nails for 5 subsequent  uses.", Quantity=nailFiveUse, Unit="kg", },
                new ConcreteworkBreakdownLine { ComponentName="Nails for 6 uses.", Quantity=nailSixUse, Unit="kg", UnitPrice=nailPrice, TotalPrice=totalNailPrice },

                new ConcreteworkBreakdownLine { ComponentName="Allow for mould oil", Quantity=mouldOilSingleUse, Unit="litre/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Mould oil for 6 uses", Quantity=mouldOilSixUse, Unit="litre/m2", UnitPrice=mouldOilPrice, TotalPrice=totalMouldOilPrice },

                new ConcreteworkBreakdownLine { ComponentName="Total material cost in formwork per m2", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new ConcreteworkBreakdownLine { ComponentName="Total allowing for repetitive use.", Quantity=1, Unit="", TotalPrice=materialCostPerUse },

				//LABOUR CUTTING AND FIXING
				new ConcreteworkBreakdownLine { ComponentName="Carpenter hours", Quantity=carpenterDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Carpenter time for 5 repetitive use-times.", Quantity=carpenterDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total carpenter hours.", Quantity=totalCarpentaDurationPer6Use, Unit="hr/m2", UnitPrice=carpenterCostPerHr, TotalPrice=totalCarpenterCost },

                new ConcreteworkBreakdownLine { ComponentName="Labour hours", Quantity=labourDurationPerFirstUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Labour time for 5 repetitive use-times.", Quantity=labourDurationPerFiveUse, Unit="hr/m2", },
                new ConcreteworkBreakdownLine { ComponentName="Total labour hours.", Quantity=totalLabourDurationPer6Use, Unit="hr/m2", UnitPrice=labourCostPerHr, TotalPrice=totalLabourCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Supervision Labour", Quantity=headmanDurationPerSqm, Unit="hr/m2", UnitPrice=headmanLabourCost, TotalPrice=totalHeadmanCost },

                new ConcreteworkBreakdownLine { ComponentName="Sub-total:", Quantity=1, Unit="", TotalPrice=totalLabourCostForSixUse },



                new ConcreteworkBreakdownLine { ComponentName="Total: Six Use ", Quantity=6, Unit="m2", TotalPrice=totalSixUse },
                new ConcreteworkBreakdownLine { ComponentName="Total: Per Use", Quantity=1, Unit="m2", TotalPrice=netCostPerTon },
            };

            return new ConcreteworkItem
            {
                ItemNo = 28,
                Description = "Sawn formwork to vertical sides of columns to form fair faced concrete not exceeding 3.00mm height from ground floor level. Six (6) number maximum uses.",
                Unit = "m2",
                NetCost = Math.Round(netCostPerTon, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem29()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 29, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 29, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("EMACO S88CA (imported shrinkage compensated cement for concrete repair)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 29, "Emaco S88CA Cement", 10.8);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 29, "Sand", 0.38);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 29, "Granite (including transportation)", 0.75);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 29, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 29, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 29, "Placing crew - labour.", 12);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 29, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 29, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Emaco S88CA Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 29,
                Description = "Concrete (1:1:2) grade 30 for repair works in water logged areas, utilising imported shrinkage compensated cement",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem30()
        {
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 30, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            //double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 30, "Cement", 28.82);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 30, "Sand", 10);
            //double stonePerM3 = 0.75;
            double wastePer = UserRateEditStore.Current.Qty(SectionKey, 30, "Add for waste.", 25);

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            //double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            double materialCostPerCum = finalMaterialCost/11;

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


            double netCostPerm3 = materialCostPerCum + (plantLabourQty * netCostPerUse);

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="", TotalPrice=plantLabourQty * netCostPerUse },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 30,
                Description = "Cement Soil, mix (1:10)",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }
        private ConcreteworkItem ComputeItem31()
        {
            double wasteQty = UserRateEditStore.Current.Qty(SectionKey, 31, "Add for waste.", 1);
            double plantLabourQty = UserRateEditStore.Current.Qty(SectionKey, 31, "Cost of plant and labour as before calculated.", 1);

            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");
            double stonePrice = GetMaterialPrice("Washed gravel (local)");
            //double stonePrice = GetMaterialPrice("15-25mm") + GetMaterialPrice("Hardcore filling") * 0.474;

            double cementPerM3 = UserRateEditStore.Current.Qty(SectionKey, 31, "Cement", 24);
            double sandPerM3 = UserRateEditStore.Current.Qty(SectionKey, 31, "Sand", 0.21);
            double stonePerM3 = UserRateEditStore.Current.Qty(SectionKey, 31, "Granite (including transportation)", 0.43);
            double wastePer = 5;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;
            double stoneCost = stonePerM3 * stonePrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost + stoneCost;
            double waste = wasteQty * totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            //LABOUR COST
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 30;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (2 * operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double plantCostPerm3 = costPerHr / volPerHr;

            double mixingCrewduration = UserRateEditStore.Current.Qty(SectionKey, 31, "Mixing crew - labour.", 9);
            double mixingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalMixingCost = mixingCrewduration * mixingCrewCost;

            double finalMixing = (plantLabourQty * plantCostPerm3) + totalMixingCost;

            //PLACING AND FINISHING COST
            double pokerVibratorDurationPerM3 = UserRateEditStore.Current.Qty(SectionKey, 31, "Poker vibrator", 0.24);
            double pokerVibratorCost = GetLabourRate("Poker vibrator (mechanical)") / 8;
            double totalPokerVibratorCost = pokerVibratorDurationPerM3 * pokerVibratorCost;

            double placingCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 31, "Placing crew - labour.", 12);
            double placingCrewCost = ((GetLabourRate("Labourer") / 8) * 1.4);
            double totalPlacingCrewCost = placingCrewCost * placingCrewPerHr;

            double masonCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 31, "Mason.", 2);
            double masonCrewCost = ((GetLabourRate("Skilled/Artisan") / 8) * 1.4);
            double totalMasonCrewCost = masonCrewCost * masonCrewPerHr;

            double headmanCrewPerHr = UserRateEditStore.Current.Qty(SectionKey, 31, "Headman", 1);
            double headmanCrewCost = ((GetLabourRate("Headman") / 8) * 1.4);
            double totalHeadmanCrewCost = headmanCrewCost * headmanCrewPerHr;

            double totalPlacingCost = totalPokerVibratorCost + totalPlacingCrewCost + totalMasonCrewCost + totalHeadmanCrewCost;

            double netCostPerm3 = finalMaterialCost + finalMixing + totalPlacingCost;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<ConcreteworkBreakdownLine>
            {
				//MATERIALCOST
				new ConcreteworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new ConcreteworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new ConcreteworkBreakdownLine { ComponentName="Granite (including transportation)", Quantity=stonePerM3, Unit="m3", UnitPrice=stonePrice, TotalPrice=stoneCost },
                new ConcreteworkBreakdownLine { ComponentName="Add for waste.", Quantity=wasteQty, Unit="5%", TotalPrice=waste },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Material Per m3", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

				//MIXING
				new ConcreteworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=plantLabourQty, Unit="m3/hr", UnitPrice=plantCostPerm3, TotalPrice=plantLabourQty * plantCostPerm3 },
                new ConcreteworkBreakdownLine { ComponentName="Mixing crew - labour.", Quantity=mixingCrewduration, Unit="per Hr", UnitPrice=mixingCrewCost, TotalPrice=totalMixingCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Mixing Per m3", Quantity=1, Unit="", TotalPrice=finalMixing },

				//PLACING
				new ConcreteworkBreakdownLine { ComponentName="Poker vibrator", Quantity=pokerVibratorDurationPerM3, Unit="hr/m3", UnitPrice=pokerVibratorCost, TotalPrice=totalPokerVibratorCost },
                new ConcreteworkBreakdownLine { ComponentName="Placing crew - labour.", Quantity=placingCrewPerHr, Unit="per Hr", UnitPrice=placingCrewCost, TotalPrice=totalPlacingCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Mason.", Quantity=masonCrewPerHr, Unit="per Hr", UnitPrice=masonCrewCost, TotalPrice=totalMasonCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Headman", Quantity=headmanCrewPerHr, Unit="per Hr", UnitPrice=headmanCrewCost, TotalPrice=totalHeadmanCrewCost },
                new ConcreteworkBreakdownLine { ComponentName="Sub-total: Placing Per m3", Quantity=1, Unit="", TotalPrice=totalPlacingCost },


                new ConcreteworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new ConcreteworkItem
            {
                ItemNo = 31,
                Description = "Concrete (1:1/4:1/2) grade 40 in foundation or slab.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                ConcreteBreakdownLine = breakdown
            };
        }

        #endregion

    }
}
