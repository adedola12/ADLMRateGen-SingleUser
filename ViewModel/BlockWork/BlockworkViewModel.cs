using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;

namespace ADLMRateGen.ViewModel.BlockWork
{
    public class BlockworkViewModel : ViewModelBase
    {
        private readonly GetItemsFromDB _helper;
        private readonly ConcreteViewModel _concreteViewModel;

        private const string SectionKey = SectionKeys.Blockwork;


        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;

        // Sorting / filtering helpers
        private bool _isNetCostFilterOn = false;
        private SortState _currentSort = SortState.None;

        private enum SortState { None, Overhead, TotalCost }

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

        public ObservableCollection<BlockworkItem> BlockworkItems { get; } =
            new ObservableCollection<BlockworkItem>();

        public ICollectionView BlockworkCollectionView { get; private set; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value ?? string.Empty;
                    RaisePropertyChanged();
                    BlockworkCollectionView?.Refresh();
                }
            }
        }

        public object SelectedDetail
        {
            get => _selectedDetail;
            set
            {
                if (!Equals(_selectedDetail, value))
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

        public BlockworkViewModel(
            MaterialLibraryViewModel matLib,
            LabourLibraryViewModel labourLib,
            ConcreteViewModel concreteViewModel)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);
            _concreteViewModel = concreteViewModel;

            matLib.LibraryChanged += OnLibraryChange;
            labourLib.LibraryChanged += OnLibraryChange;

            // ✅ Load pushed compute-items at least once (otherwise Items stays empty)
            ComputeCatalogStore.ReloadFromDisk();

            // Build list (built-in + pushed)
            BuildBlockWorkItem();

            BlockworkCollectionView = CollectionViewSource.GetDefaultView(BlockworkItems);
            BlockworkCollectionView.Filter = FilterBlockWorkItem;

            RecomputeCommand = new DelegateCommand(_ => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
            FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
            SortCommand = new DelegateCommand(_ => CycleSort());
            AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());

            // ✅ When admin pushes new compute-items.json and your app reloads it,
            // this event must rebuild the list.
            ComputeCatalogStore.Changed += LoadComputeItems;

            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CurrencyService.Rate) ||
                    e.PropertyName == nameof(CurrencyService.Code))
                {
                    RecomputeAll();
                }
            };
        }

        // -------------------- core UI actions --------------------

        private void ShowDetails(object o)
        {
            if (o is BlockworkItem item)
            {
                var detailedControl = new BlockworkItemDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () => { SelectedDetail = null; };
                SelectedDetail = detailedControl;
            }
        }

        private bool FilterBlockWorkItem(object obj)
        {
            if (!(obj is BlockworkItem item)) return false;
            if (string.IsNullOrWhiteSpace(SearchTerm)) return true;

            return (item.Description ?? string.Empty)
                .IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RecomputeAll()
        {
            BlockworkItems.Clear();
            BuildBlockWorkItem();
            BlockworkCollectionView?.Refresh();
        }

        private void OnLibraryChange()
        {
            RecomputeAll();
        }

        private void ToggleNetCostFilter()
        {
            _isNetCostFilterOn = !_isNetCostFilterOn;

            BlockworkCollectionView.SortDescriptions.Clear();
            if (_isNetCostFilterOn)
            {
                BlockworkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(BlockworkItem.NetCost), ListSortDirection.Ascending));
            }
        }

        private void CycleSort()
        {
            // cycle: None -> Overhead -> TotalCost -> None
            if (_currentSort == SortState.None) _currentSort = SortState.Overhead;
            else if (_currentSort == SortState.Overhead) _currentSort = SortState.TotalCost;
            else _currentSort = SortState.None;

            BlockworkCollectionView.SortDescriptions.Clear();

            if (_currentSort == SortState.Overhead)
            {
                BlockworkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(BlockworkItem.OverheadValue), ListSortDirection.Ascending));
            }
            else if (_currentSort == SortState.TotalCost)
            {
                BlockworkCollectionView.SortDescriptions.Add(
                    new SortDescription(nameof(BlockworkItem.TotalCost), ListSortDirection.Ascending));
            }
        }

        private void OpenCustomRateEntry()
        {
            var view = new CustomRateEntryView();
            view.DataContext = new CustomRateEntryViewModel();
            SelectedDetail = view;
        }

        // -------------------- pushed compute-items wiring --------------------

        // ✅ Fixes CS0103 and ensures pushed items rebuild the view
        private void LoadComputeItems()
        {
            // IMPORTANT: Do NOT call ReloadFromDisk() here (it raises Changed again)
            RecomputeAll();
        }

        private void BuildBlockWorkItem()
        {
            // 1) built-in items
            AddBuiltInItems();

            // 2) pushed compute catalog items
            AppendComputeCatalogItems();
        }

        private void AddBuiltInItems()
        {
            Func<BlockworkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3, ComputeItem4, ComputeItem5, ComputeItem6,
                ComputeItem7, ComputeItem8, ComputeItem9, ComputeItem10, ComputeItem11, ComputeItem12,
                ComputeItem13, ComputeItem14, ComputeItem15, ComputeItem16, ComputeItem17, ComputeItem18
            };

            foreach (var compute in computeMethods)
                BlockworkItems.Add(compute());
        }

        private void AppendComputeCatalogItems()
        {
            var defs = ComputeCatalogStore.Items
                .Where(d => d != null && d.enabled)
                .Where(d => SectionNormalizer.ToSectionKey(d.section) == SectionKey)
                .ToList();

            if (defs.Count == 0) return;

            int nextNo = BlockworkItems.Count + 1;

            foreach (var def in defs)
            {
                var item = BuildBlockworkItemFromDefinition(def, nextNo++);
                if (item != null)
                    BlockworkItems.Add(item);
            }
        }

        private BlockworkItem BuildBlockworkItemFromDefinition(ComputeItemDefinition def, int itemNo)
        {
            if (def == null) return null;

            double net = 0.0;
            var breakdown = new ObservableCollection<BlockworkBreakdownLine>();

            var lines = def.lines ?? new List<ComputeLine>();
            foreach (var ln in lines)
            {
                if (ln == null) continue;

                double qty = (double)ln.qtyPerUnit;
                double factor = (double)(ln.factor == 0 ? 1 : ln.factor);

                double unitPrice = ResolveLineUnitPrice(ln);
                double lineTotal = qty * factor * unitPrice;

                net += lineTotal;

                breakdown.Add(new BlockworkBreakdownLine
                {
                    ComponentName = string.IsNullOrWhiteSpace(ln.description) ? def.name : ln.description,
                    Quantity = Math.Round(qty * factor, 6),
                    Unit = ln.unit ?? "",
                    UnitPrice = Math.Round(unitPrice, 2),
                    TotalPrice = Math.Round(lineTotal, 2)
                });
            }

            var ohp = ApplyOHP(net);

            return new BlockworkItem
            {
                ItemNo = itemNo,
                Description = def.name ?? "Custom Item",
                Unit = string.IsNullOrWhiteSpace(def.outputUnit) ? "m2" : def.outputUnit,
                NetCost = Math.Round(net, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }

        private double ResolveLineUnitPrice(ComputeLine ln)
        {
            string kind = (ln.kind ?? "").Trim().ToLowerInvariant();

            // If backend cached a unit price preview, use as fallback (not primary)
            double cached = ln.unitPriceAtBuild.HasValue ? (double)ln.unitPriceAtBuild.Value : 0.0;

            // Try resolve by SN if your helper supports it (reflection-safe)
            if (ln.refSn.HasValue && ln.refSn.Value > 0)
            {
                int sn = ln.refSn.Value;

                if (kind == "material")
                {
                    double bySn = TryInvokePriceBySn(_helper, new[]
                    {
                        "GetMaterialPriceBySn", "GetMaterialPriceBySN", "GetMaterialPriceByRefSn"
                    }, sn);

                    if (!double.IsNaN(bySn)) return bySn;
                }
                else if (kind == "labour")
                {
                    double bySn = TryInvokePriceBySn(_helper, new[]
                    {
                        "GetLabourRateBySn", "GetLabourRateBySN", "GetLabourRateByRefSn"
                    }, sn);

                    if (!double.IsNaN(bySn)) return bySn;
                }
            }

            // Fallback: resolve by description name (works if you store names matching your library)
            if (kind == "material")
                return _helper.GetMaterialPrice(ln.description);

            if (kind == "labour")
                return _helper.GetLabourRate(ln.description);

            // "constant" or unknown kind
            return cached;
        }

        private static double TryInvokePriceBySn(object target, string[] methodNames, int sn)
        {
            try
            {
                var t = target.GetType();
                foreach (var name in methodNames)
                {
                    var mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(int) }, null);

                    if (mi == null) continue;

                    var res = mi.Invoke(target, new object[] { sn });
                    if (res == null) continue;

                    return Convert.ToDouble(res);
                }
            }
            catch
            {
                // ignore; we return NaN if not supported
            }

            return double.NaN;
        }

        // -------------------- existing helpers (unchanged) --------------------

        private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
        {
            double ov = netCost * (OverheadPercent / 100);
            double pv = netCost * (ProfitPercent / 100);
            double total = netCost + ov + pv;
            return (ov, pv, total);
        }

        private double GetMaterialPrice(string name) => _helper.GetMaterialPrice(name);
        private double GetLabourRate(string name) => _helper.GetLabourRate(name);

        public double GetNetValue(Func<BlockworkItem> computeItemFunc)
        {
            var item = computeItemFunc();
            return item.NetCost;
        }

        public double GetConcreteNetValue(Func<ConcreteworkItem> computeFunc)
        {
            return _concreteViewModel.GetConcreteNetValue(computeFunc);
        }

        public double GetBlockworkNetValue(Func<ConcreteworkItem> computeFunc)
        {
            return computeFunc().NetCost;
        }

        // -------------------- YOUR EXISTING ComputeItem1..ComputeItem18 METHODS GO HERE --------------------

        #region Compute Methods
        private BlockworkItem ComputeItem1()
        {
            double mixerCost = GetLabourRate("Concrete mixer 10/7");
            double dieselPrice = (GetLabourRate("Labourer") / 8) * 1.4;
            double literPerDay = 40;
            double fuelCost = dieselPrice * literPerDay;
            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;

            double totalPlantDay = mixerCost + fuelCost +
                (0.03 * fuelCost) + (operatorCost);

            double workHr = 8;
            double costPerHr = totalPlantDay / workHr;

            double volPerHr = 5.66;
            double netCostPerm3 = costPerHr / volPerHr;
            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
                new BlockworkBreakdownLine { ComponentName="Concrete mixer 10/7", Quantity=1, Unit="N/day", UnitPrice=mixerCost },
                new BlockworkBreakdownLine { ComponentName="Fuel (Diesel)", Quantity=literPerDay, Unit="hr/m3", UnitPrice=dieselPrice, TotalPrice=fuelCost },
                new BlockworkBreakdownLine { ComponentName="Oil and consumables (per day)", Quantity=3, Unit="%", TotalPrice=0.03 * fuelCost },
                new BlockworkBreakdownLine { ComponentName="Operator (per day)", Quantity=1, Unit="Nr/Day", UnitPrice=operatorCost, TotalPrice=operatorCost },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Cost per day", Quantity=1, Unit="", TotalPrice=totalPlantDay },
                new BlockworkBreakdownLine { ComponentName="Cost per hour (8 hour Working Day)", Quantity=workHr, Unit="N/Hr", UnitPrice=totalPlantDay, TotalPrice=costPerHr },
                new BlockworkBreakdownLine { ComponentName="Total", Quantity=volPerHr, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new BlockworkItem
            {
                ItemNo = 1,
                Description = "Calculating plant and labour cost for mixing concrete or mortar, using 14/10 mixer. Note mixer is running a 3 minute circle prior to pouring out mix.",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        public BlockworkItem ComputeItem2()
        {
            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");

            double cementPerM3 = 28.82;
            double sandPerM3 = 3;
            double wastePer = 25;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            double materialCostPerCum = finalMaterialCost / 4;

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


            double netCostPerm3 = materialCostPerCum + netCostPerUse;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new BlockworkItem
            {
                ItemNo = 2,
                Description = "Mortar Mix (1:3)",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        public BlockworkItem ComputeItem3()
        {
            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");

            double cementPerM3 = 28.82;
            double sandPerM3 = 4;
            double wastePer = 25;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            double materialCostPerCum = finalMaterialCost / 5;

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


            double netCostPerm3 = materialCostPerCum + netCostPerUse;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new BlockworkItem
            {
                ItemNo = 3,
                Description = "Mortar Mix (1:4)",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        public BlockworkItem ComputeItem4()
        {
            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");

            double cementPerM3 = 28.82;
            double sandPerM3 = 6;
            double wastePer = 25;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            double materialCostPerCum = finalMaterialCost / 7;

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


            double netCostPerm3 = materialCostPerCum + netCostPerUse;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new BlockworkItem
            {
                ItemNo = 4,
                Description = "Mortar Mix (1:6)",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        public BlockworkItem ComputeItem5()
        {
            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");

            double cementPerM3 = 28.82;
            double sandPerM3 = 1;
            double wastePer = 25;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            double materialCostPerCum = finalMaterialCost / 2;

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


            double netCostPerm3 = materialCostPerCum + netCostPerUse;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new BlockworkItem
            {
                ItemNo = 5,
                Description = "Mortar Mix (1:1)",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem6()
        {
            //MATERIAL COST
            double cementPrice = GetMaterialPrice("Cement (50kg bag)");
            double cementLoadingPrice = GetMaterialPrice("Loading and unloading cement");
            double sandPrice = GetMaterialPrice("Sharp Sand");

            double cementPerM3 = 28.82;
            double sandPerM3 = 12;
            double wastePer = 25;

            double cementCost = cementPerM3 * cementPrice;
            double cementLoadingCost = cementLoadingPrice * cementPerM3;
            double sandCost = sandPerM3 * sandPrice;

            double totalMaterialCost = cementCost + cementLoadingCost + sandCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste;

            double materialCostPerCum = finalMaterialCost / 13;

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


            double netCostPerm3 = materialCostPerCum + netCostPerUse;

            var ohp = ApplyOHP(netCostPerm3);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementPrice, TotalPrice=cementCost },
                new BlockworkBreakdownLine { ComponentName="Loading and Unloading cement", Quantity=cementPerM3, Unit="bag/m3", UnitPrice=cementLoadingPrice, TotalPrice=cementLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Sand", Quantity=sandPerM3, Unit="m3", UnitPrice=sandPrice, TotalPrice=sandCost},
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=totalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material cost per m3", Quantity=1, Unit="m3", TotalPrice=materialCostPerCum },
			
				//MIXING
				new BlockworkBreakdownLine { ComponentName="Cost of plant and labour as before calculated.", Quantity=1, Unit="", TotalPrice=netCostPerUse },


                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m3", TotalPrice=netCostPerm3 },
            };

            return new BlockworkItem
            {
                ItemNo = 6,
                Description = "Mortar Mix (1:12)",
                Unit = "m3",
                NetCost = Math.Round(netCostPerm3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem7()
        {
            //MATERIAL COST
            double blockPrice = GetMaterialPrice("225 x 225 x 450mm (9 x 9 x 18\") Hollow blocks");
            double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

            double blockPerM2 = 10;
            double wastePer = 10;

            double blockCost = blockPrice * blockPerM2;
            double blockLoadingCost = blockLoadingPrice * blockPerM2;

            double totalMaterialCost = blockCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste+ blockLoadingCost;

            double mortarCost = GetNetValue(ComputeItem4);
            double mortarQty = 0.013;
            double totalMortarCost = mortarCost * mortarQty;
            double mortarWastePer = 5;
            double mortarWaste = totalMortarCost * (mortarWastePer / 100);
            double finalCost = totalMortarCost + mortarWaste;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 140;
            double areaPerDay = outputPerDay / blockPerM2;
            double labourCostPerM2 = totalLabourCost/areaPerDay;

            double netCostPerm2 = finalMaterialCost+finalCost+labourCostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
                new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Output 140 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 7,
                Description = "225mm blockwall in cement and sand mortar (1:6)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem8()
        {
            double concreteFillingQty = 0.1030;
            double concreteCost = GetConcreteNetValue(_concreteViewModel.ComputeItem3);
            double concreteTotal = concreteFillingQty * concreteCost;

            double wastePer = 2.5;
            double waste = concreteTotal * (wastePer / 100);
            double finalMaterialCost = concreteTotal + waste;

            //LABOUR COST
            double labourCost = (GetLabourRate("Labourer")/8) * 1.4;
            double labourDuration = 0.5;
            double labourPrice = labourCost * labourDuration;

            double netFillingCost = finalMaterialCost + labourPrice;
            double blockCost = GetNetValue(ComputeItem7);

            double netCostPerm2 = netFillingCost + blockCost;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Concrete filling in 225mm blockwall", Quantity=concreteFillingQty, Unit="m3/m2", UnitPrice=concreteCost, TotalPrice=concreteTotal },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Labour filling 225mm blockwall", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourPrice },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Filling Cost", Quantity=1, Unit="", TotalPrice=netFillingCost },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 8,
                Description = "Concrete filling in 225mm blockwall",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };

        }
        private BlockworkItem ComputeItem9()
        {
            //MATERIAL COST
            double blockPrice = GetMaterialPrice("150 x 225 x 450mm (6 x 9 x 18\") Hollow blocks");
            double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

            double blockPerM2 = 10;
            double wastePer = 10;

            double blockCost = blockPrice * blockPerM2;
            double blockLoadingCost = blockLoadingPrice * blockPerM2;

            double totalMaterialCost = blockCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

            double mortarCost = GetNetValue(ComputeItem4);
            double mortarQty = 0.0084;
            double totalMortarCost = mortarCost * mortarQty;
            double mortarWastePer = 5;
            double mortarWaste = totalMortarCost * (mortarWastePer / 100);
            double finalCost = totalMortarCost + mortarWaste;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 160;
            double areaPerDay = outputPerDay / blockPerM2;
            double labourCostPerM2 = totalLabourCost / areaPerDay;

            double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
                new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 9,
                Description = "150mm blockwall in cement and sand mortar (1:6)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem10()
        {
            double concreteFillingQty = 0.0618;
            double concreteCost = GetConcreteNetValue(_concreteViewModel.ComputeItem3);
            double concreteTotal = concreteFillingQty * concreteCost;

            double wastePer = 2.5;
            double waste = concreteTotal * (wastePer / 100);
            double finalMaterialCost = concreteTotal + waste;

            //LABOUR COST
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double labourDuration = 0.33;
            double labourPrice = labourCost * labourDuration;

            double netFillingCost = finalMaterialCost + labourPrice;
            double blockCost = GetNetValue(ComputeItem7);

            double netCostPerm2 = netFillingCost + blockCost;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Concrete filling in 150mm blockwall", Quantity=concreteFillingQty, Unit="m3/m2", UnitPrice=concreteCost, TotalPrice=concreteTotal },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Labour filling 150mm blockwall", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourPrice },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Filling Cost", Quantity=1, Unit="", TotalPrice=netFillingCost },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 10,
                Description = "Concrete filling in 150mm blockwall",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem11()
        {
            //MATERIAL COST
            double blockPrice = GetMaterialPrice("100 x 225 x 450mm (4 x 9 x 18\") Hollow blocks");
            double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

            double blockPerM2 = 10;
            double wastePer = 10;

            double blockCost = blockPrice * blockPerM2;
            double blockLoadingCost = blockLoadingPrice * blockPerM2;

            double totalMaterialCost = blockCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

            double mortarCost = GetNetValue(ComputeItem4);
            double mortarQty = 0.0058;
            double totalMortarCost = mortarCost * mortarQty;
            double mortarWastePer = 5;
            double mortarWaste = totalMortarCost * (mortarWastePer / 100);
            double finalCost = totalMortarCost + mortarWaste;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 200;
            double areaPerDay = outputPerDay / blockPerM2;
            double labourCostPerM2 = totalLabourCost / areaPerDay;

            double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
                new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 11,
                Description = "100mm blockwall in cement and sand mortar (1:6)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem12()
        {
            double concreteFillingQty = 0.0442;
            double concreteCost = GetConcreteNetValue(_concreteViewModel.ComputeItem3);
            double concreteTotal = concreteFillingQty * concreteCost;

            double wastePer = 2.5;
            double waste = concreteTotal * (wastePer / 100);
            double finalMaterialCost = concreteTotal + waste;

            //LABOUR COST
            double labourCost = (GetLabourRate("Labourer") / 8) * 1.4;
            double labourDuration = 0.25;
            double labourPrice = labourCost * labourDuration;

            double netFillingCost = finalMaterialCost + labourPrice;
            double blockCost = GetNetValue(ComputeItem7);

            double netCostPerm2 = netFillingCost + blockCost;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Concrete filling in 100mm blockwall", Quantity=concreteFillingQty, Unit="m3/m2", UnitPrice=concreteCost, TotalPrice=concreteTotal },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Labour filling 100mm blockwall", Quantity=labourDuration, Unit="hr/m2", UnitPrice=labourCost, TotalPrice=labourPrice },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Filling Cost", Quantity=1, Unit="", TotalPrice=netFillingCost },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 12,
                Description = "Concrete filling in 100mm blockwall",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem13()
        {
            //MATERIAL COST
            double blockPrice = GetMaterialPrice("225 x 225 x 450mm (9 x 9 x 18\") Hollow blocks");
            double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

            double blockPerM2 = 10;
            double wastePer = 10;

            double blockCost = blockPrice * blockPerM2;
            double blockLoadingCost = blockLoadingPrice * blockPerM2;

            double totalMaterialCost = blockCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

            double mortarCost = GetNetValue(ComputeItem3);
            double mortarQty = 0.013;
            double totalMortarCost = mortarCost * mortarQty;
            double mortarWastePer = 5;
            double mortarWaste = totalMortarCost * (mortarWastePer / 100);
            double finalCost = totalMortarCost + mortarWaste;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 140;
            double areaPerDay = outputPerDay / blockPerM2;
            double labourCostPerM2 = totalLabourCost / areaPerDay;

            double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
                new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Output 140 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 13,
                Description = "225mm blockwall in cement and sand mortar (1:4)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem14()
        {
            //MATERIAL COST
            double blockPrice = GetMaterialPrice("150 x 225 x 450mm (6 x 9 x 18\") Hollow blocks");
            double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

            double blockPerM2 = 10;
            double wastePer = 10;

            double blockCost = blockPrice * blockPerM2;
            double blockLoadingCost = blockLoadingPrice * blockPerM2;

            double totalMaterialCost = blockCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

            double mortarCost = GetNetValue(ComputeItem3);
            double mortarQty = 0.0084;
            double totalMortarCost = mortarCost * mortarQty;
            double mortarWastePer = 5;
            double mortarWaste = totalMortarCost * (mortarWastePer / 100);
            double finalCost = totalMortarCost + mortarWaste;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 160;
            double areaPerDay = outputPerDay / blockPerM2;
            double labourCostPerM2 = totalLabourCost / areaPerDay;

            double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
                new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 14,
                Description = "150mm blockwall in cement and sand mortar (1:4)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem15()
        {
            //MATERIAL COST
            double blockPrice = GetMaterialPrice("100 x 225 x 450mm (4 x 9 x 18\") Hollow blocks");
            double blockLoadingPrice = GetMaterialPrice("Loading and unloading blocks");

            double blockPerM2 = 10;
            double wastePer = 10;

            double blockCost = blockPrice * blockPerM2;
            double blockLoadingCost = blockLoadingPrice * blockPerM2;

            double totalMaterialCost = blockCost;
            double waste = totalMaterialCost * (wastePer / 100);
            double finalMaterialCost = totalMaterialCost + waste + blockLoadingCost;

            double mortarCost = GetNetValue(ComputeItem3);
            double mortarQty = 0.0058;
            double totalMortarCost = mortarCost * mortarQty;
            double mortarWastePer = 5;
            double mortarWaste = totalMortarCost * (mortarWastePer / 100);
            double finalCost = totalMortarCost + mortarWaste;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 200;
            double areaPerDay = outputPerDay / blockPerM2;
            double labourCostPerM2 = totalLabourCost / areaPerDay;

            double netCostPerm2 = finalMaterialCost + finalCost + labourCostPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="Blocks per square meter.", Quantity=blockPerM2, Unit="No", UnitPrice=blockPrice, TotalPrice=blockCost },
                new BlockworkBreakdownLine { ComponentName="Loading and unloading blocks", Quantity=blockPerM2, Unit="No", UnitPrice=blockLoadingPrice, TotalPrice=blockLoadingCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Block", Quantity=1, Unit="", TotalPrice=finalMaterialCost },

                new BlockworkBreakdownLine { ComponentName="Mortar per square meter", Quantity=mortarQty, Unit="m3/m2", UnitPrice=mortarCost, TotalPrice=totalMortarCost },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=mortarWastePer, Unit="%", TotalPrice=mortarWaste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Mortar", Quantity=1, Unit="", TotalPrice=finalCost },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Labour cost per day", Quantity=1, Unit="per day", TotalPrice=totalLabourCost },

                new BlockworkBreakdownLine { ComponentName="Sub-total: Output 160 blocks per day @ 10 blocks/m2", Quantity=areaPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourCostPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 15,
                Description = "100mm blockwall in cement and sand mortar (1:4)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem16()
        {
            //MATERIAL COST
            double plywoodPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
            double hardwoodPrice = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
            double solignumPrice = GetMaterialPrice("Solignum (normal)")/20;

            double panelPer100m2 = 35;
            double hardwoodPer100m2 = 130;
            double solignumPer100m2 = 50;

            double panelCost = plywoodPrice * panelPer100m2;
            double hardwoodCost = hardwoodPer100m2 * hardwoodPrice;
            double solignumCost = solignumPrice * solignumPer100m2;
            double nailPer = 2.5;
            double nails = (panelCost+hardwoodCost) * (nailPer / 100);
            double wastePer = 5;
            double waste = (panelCost + hardwoodCost+solignumCost+nails) * (wastePer / 100);

            double totalMaterial = panelCost + hardwoodCost + solignumCost + nails + waste;
            double materialPerM2 = totalMaterial / 100;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 30;
            double labourPerM2 = totalLabourCost / outputPerDay;

            double netCostPerm2 = materialPerM2 + labourPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="18mm plywood", Quantity=panelPer100m2, Unit="No", UnitPrice=plywoodPrice, TotalPrice=panelCost },
                new BlockworkBreakdownLine { ComponentName="50 x 50mm hardwood", Quantity=hardwoodPer100m2, Unit="No", UnitPrice=hardwoodPrice, TotalPrice=hardwoodCost },
                new BlockworkBreakdownLine { ComponentName="Solignum", Quantity=solignumPer100m2, Unit="No", UnitPrice=solignumPrice, TotalPrice=solignumCost },
                new BlockworkBreakdownLine { ComponentName="Add for nail.", Quantity=nailPer, Unit="%", TotalPrice=nails },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material per 100m2", Quantity=1, Unit="", TotalPrice=totalMaterial },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material per m2", Quantity=1, Unit="", TotalPrice=materialPerM2 },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Output 30m2 per day", Quantity=outputPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 16,
                Description = "Single face timber paneling to wall comprising 18mm plywood, and 50 x 50mm timber framing at 600mm centers and including treating with solignum (Panel area to be 100m2)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem17()
        {
            //MATERIAL COST
            double plywoodPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
            double hardwoodPrice = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
            double solignumPrice = GetMaterialPrice("Solignum (normal)") / 20;

            double panelPer100m2 = 35;
            double hardwoodPer100m2 = 80;
            double solignumPer100m2 = 50;

            double panelCost = plywoodPrice * panelPer100m2;
            double hardwoodCost = hardwoodPer100m2 * hardwoodPrice;
            double solignumCost = solignumPrice * solignumPer100m2;
            double nailPer = 2.5;
            double nails = (panelCost + hardwoodCost) * (nailPer / 100);
            double wastePer = 5;
            double waste = (panelCost + hardwoodCost + solignumCost + nails) * (wastePer / 100);

            double totalMaterial = panelCost + hardwoodCost + solignumCost + nails + waste;
            double materialPerM2 = totalMaterial / 100;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 40;
            double labourPerM2 = totalLabourCost / outputPerDay;

            double netCostPerm2 = materialPerM2 + labourPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="18mm plywood", Quantity=panelPer100m2, Unit="No", UnitPrice=plywoodPrice, TotalPrice=panelCost },
                new BlockworkBreakdownLine { ComponentName="50 x 50mm hardwood", Quantity=hardwoodPer100m2, Unit="No", UnitPrice=hardwoodPrice, TotalPrice=hardwoodCost },
                new BlockworkBreakdownLine { ComponentName="Solignum", Quantity=solignumPer100m2, Unit="No", UnitPrice=solignumPrice, TotalPrice=solignumCost },
                new BlockworkBreakdownLine { ComponentName="Add for nail.", Quantity=nailPer, Unit="%", TotalPrice=nails },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material per 100m2", Quantity=1, Unit="", TotalPrice=totalMaterial },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material per m2", Quantity=1, Unit="", TotalPrice=materialPerM2 },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Output 30m2 per day", Quantity=outputPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 17,
                Description = "Single face timber paneling to wall comprising 18mm plywood, and 50 x 50mm timber framing at 1200mm centers and including treating with solignum (Panel area to be 100m2)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }
        private BlockworkItem ComputeItem18()
        {
            //MATERIAL COST
            double plywoodPrice = GetMaterialPrice("3/4\"x4x8'(18x1200x2400mm)");
            double hardwoodPrice = GetMaterialPrice("2x2\"x12' (50x50x3600mm) - Hardwood");
            double solignumPrice = GetMaterialPrice("Solignum (normal)") / 20;

            double panelPer100m2 = 70;
            double hardwoodPer100m2 = 80;
            double solignumPer100m2 = 50;

            double panelCost = plywoodPrice * panelPer100m2;
            double hardwoodCost = hardwoodPer100m2 * hardwoodPrice;
            double solignumCost = solignumPrice * solignumPer100m2;
            double nailPer = 2.5;
            double nails = (panelCost + hardwoodCost) * (nailPer / 100);
            double wastePer = 5;
            double waste = (panelCost + hardwoodCost + solignumCost + nails) * (wastePer / 100);

            double totalMaterial = panelCost + hardwoodCost + solignumCost + nails + waste;
            double materialPerM2 = totalMaterial / 100;

            //LABOUR COST
            double masonCost = GetLabourRate("Skilled/Artisan") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double masonQty = 3;
            double labourQty = 2;

            double masonPrice = masonCost * masonQty;
            double labourPrice = labourCost * labourQty;
            double totalLabourCost = masonPrice + labourPrice;

            double outputPerDay = 25;
            double labourPerM2 = totalLabourCost / outputPerDay;

            double netCostPerm2 = materialPerM2 + labourPerM2;

            var ohp = ApplyOHP(netCostPerm2);

            var breakdown = new ObservableCollection<BlockworkBreakdownLine>
            {
				//MATERIALCOST
				new BlockworkBreakdownLine { ComponentName="18mm plywood", Quantity=panelPer100m2, Unit="No", UnitPrice=plywoodPrice, TotalPrice=panelCost },
                new BlockworkBreakdownLine { ComponentName="50 x 50mm hardwood", Quantity=hardwoodPer100m2, Unit="No", UnitPrice=hardwoodPrice, TotalPrice=hardwoodCost },
                new BlockworkBreakdownLine { ComponentName="Solignum", Quantity=solignumPer100m2, Unit="No", UnitPrice=solignumPrice, TotalPrice=solignumCost },
                new BlockworkBreakdownLine { ComponentName="Add for nail.", Quantity=nailPer, Unit="%", TotalPrice=nails },
                new BlockworkBreakdownLine { ComponentName="Add for waste.", Quantity=wastePer, Unit="%", TotalPrice=waste },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material per 100m2", Quantity=1, Unit="", TotalPrice=totalMaterial },
                new BlockworkBreakdownLine { ComponentName="Sub-total: Material per m2", Quantity=1, Unit="", TotalPrice=materialPerM2 },

                new BlockworkBreakdownLine { ComponentName="Masons", Quantity=masonQty, Unit="per day", UnitPrice=masonCost, TotalPrice=masonPrice },
                new BlockworkBreakdownLine { ComponentName="Labour", Quantity=labourQty, Unit="per day", UnitPrice=labourCost, TotalPrice=labourPrice },
                new BlockworkBreakdownLine { ComponentName="Output 30m2 per day", Quantity=outputPerDay, Unit="m2", UnitPrice=totalLabourCost, TotalPrice=labourPerM2 },

                new BlockworkBreakdownLine { ComponentName="Total", Quantity=1, Unit="m2", TotalPrice=netCostPerm2 },
            };

            return new BlockworkItem
            {
                ItemNo = 18,
                Description = "Double face timber paneling to wall comprising 18mm plywood, and 50 x 50mm timber framing at 1200mm centers and including treating with solignum (Panel area to be 100m2)",
                Unit = "m2",
                NetCost = Math.Round(netCostPerm2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
                BlockworkBreakdownLine = breakdown
            };
        }

        #endregion



    }
}
