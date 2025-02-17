using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.View;

namespace ADLMRateGen.ViewModel.Groundwork
{
    public class GroundWorkViewModel: ViewModelBase
    {
        private readonly MaterialLibraryViewModel _materialLib;
        private readonly LabourLibraryViewModel _labourLib;

        private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;


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
        public ICollectionView GroundworkCollectionView { get;private set; }
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
        public GroundWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _materialLib = matLib;
            _labourLib = labourLib;

            _materialLib.LibraryChanged += OnLibraryChanged;
            _labourLib.LibraryChanged += OnLibraryChanged;

            BuildGroundWorkItems();

            GroundworkCollectionView = CollectionViewSource.GetDefaultView(GroundworkItems);
            GroundworkCollectionView.Filter = FilterGroundWorkItem;

            RecomputeCommand = new DelegateCommand(o => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
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
          if(obj is GroundworkItem item)
            {
                if (string.IsNullOrEmpty(SearchTerm))
                    return true;
                return item.Description?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        private void OnLibraryChanged()
        {
            RecomputeAll();
        }
        private void RecomputeAll()
        {
            GroundworkItems.Clear();
            BuildGroundWorkItems();
        }
        private void BuildGroundWorkItems()
        {
            GroundworkItems.Add(ComputeItem1());
            GroundworkItems.Add(ComputeItem2());
            GroundworkItems.Add(ComputeItem3());
            GroundworkItems.Add(ComputeItem4());
            GroundworkItems.Add(ComputeItem5());
            GroundworkItems.Add(ComputeItem6());
            GroundworkItems.Add(ComputeItem7());
            GroundworkItems.Add(ComputeItem8());
            GroundworkItems.Add(ComputeItem9());
            GroundworkItems.Add(ComputeItem10());
            //GroundworkItems.Add(ComputeItem11());
            //GroundworkItems.Add(ComputeItem12());
            //GroundworkItems.Add(ComputeItem13());
            //GroundworkItems.Add(ComputeItem14());
            //GroundworkItems.Add(ComputeItem15());
            //GroundworkItems.Add(ComputeItem16());
            //GroundworkItems.Add(ComputeItem17());
            //GroundworkItems.Add(ComputeItem18());

        }


        private (double overheadVal, double profitVal, double total) ApplyOHP(double netCost)
        {
            double ov = netCost * (OverheadPercent / 100);
            double pv = netCost * (ProfitPercent / 100);
            double total = netCost + ov + pv;

            return(ov, pv, total);
        }

        private GroundworkItem ComputeItem1()
        {
            double d8Cost = GetLabourRate("Bulldozer D8");
            double dieselPrice = GetMaterialPrice("Diesel");
            double operatorCost = GetLabourRate("Heavy plant operator");
            double banksmanCost = GetLabourRate("Heavy vehicle driver");
            double labourCost = GetLabourRate("Semi skilled");
            double literPerDay = 304.0;
            double outputPerDay = 1456.0;

            double totalPlantDay = d8Cost + (literPerDay * dieselPrice) +
                (0.03 * (literPerDay * dieselPrice)) + operatorCost + banksmanCost + (2 * labourCost);

            double costPerM2 = totalPlantDay / outputPerDay;
            var ohp = ApplyOHP(costPerM2);

            var breakdown = new ObservableCollection<GroundworkBreakdownLine>
            {
                new GroundworkBreakdownLine
                {
                    ComponentName = "D8 Bulldozer",
                    Quantity = 1,
                    Unit = "No/Day",
                    UnitPrice = d8Cost,
                    TotalPrice = d8Cost
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
                    Quantity = 1,
                    Unit = "No/Day",
                    UnitPrice = operatorCost,
                    TotalPrice = operatorCost
                },
                new GroundworkBreakdownLine
                {
                  ComponentName = "Oil & Consumables (3% of Diesel)",
                  Quantity = 1,
                    Unit = "3%",
                    UnitPrice = 0.03 * dieselPrice,
                    TotalPrice = 0.03 * dieselPrice
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Banksman",
                    Quantity = 1,
                    Unit = "No/Day",
                    UnitPrice = banksmanCost,
                    TotalPrice = banksmanCost
                },
                new GroundworkBreakdownLine
                {
                    ComponentName = "Labour",
                    Quantity = 2,
                    Unit = "No/Day",
                    UnitPrice = labourCost,
                    TotalPrice = 2 * labourCost
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
            double subCost = ComputeItem1_SubTotal();
            double outputPerDay = 1820.0;

            double costPerM3 = subCost / outputPerDay;
            var ohp = ApplyOHP(costPerM3);

            return new GroundworkItem
            {
                ItemNo = 2,
                Description = "Excavation as before but distance not exceeding 50 meters.",
                Unit = "m3",
                NetCost = Math.Round(costPerM3, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2)
            };
        }
        private GroundworkItem ComputeItem3()
        {
            double subCost = ComputeItem1_SubTotal();
            double outputPerDay = 980.0;

            double costPerM2 = subCost / outputPerDay;
            var ohp = ApplyOHP(costPerM2);

            return new GroundworkItem
            {
                ItemNo = 3,
                Description = "Excavate oversite to remove topsoil 150mm deep using D8 or equivalent plant to distance not exceeding 50m",
                Unit = "m2",
                NetCost = Math.Round(costPerM2, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2),
            };
        }
        private GroundworkItem ComputeItem4()
        {
            double labourDuration = 1.6;
            double wheelDuration = 1.18;
            double labourCost = GetLabourRate("Skilled/Artisan");
            double ratePerHr = labourCost / 8;

            double excavateLabour = labourDuration * ratePerHr;
            double wheelLabour = wheelDuration * ratePerHr;

            double costPerM3 = excavateLabour + wheelLabour;
            double costPerM2 = costPerM3 * 0.15;
            var ohp = ApplyOHP(costPerM2);

            return new GroundworkItem
            {
                ItemNo = 4,
                Description = "Excavate oversite by hand to reduce levels in sandy soil and wheel material to dump not exceeding 20m from excavation.",
                Unit = "m2",
                NetCost = Math.Round(costPerM2, 0),
                OverheadValue = Math.Round(ohp.overheadVal,2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 0)
            };

        }
        private GroundworkItem ComputeItem5()
        {
            double workDuration = 1.4;
            double labourCost = GetLabourRate("Labourer");
            double ratePerHr = labourCost / 8;

            double costPerM3 = workDuration * ratePerHr;
            var ohp = ApplyOHP(costPerM3);

            return new GroundworkItem
            {
                ItemNo = 5,
                Description = "Excavate by hand shallow trench in soft sand for foundation not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(costPerM3, 0),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 0)
            };
        }
        private GroundworkItem ComputeItem6()
        {
            double workDuration = 1.53;
            double labourCost = GetLabourRate("Labourer");
            double ratePerHr = labourCost / 8;
            double rateWithBonus = (ratePerHr *0.4) + ratePerHr;

            double costPerM3 = workDuration * rateWithBonus;
            var ohp = ApplyOHP(costPerM3);

            return new GroundworkItem
            {
                ItemNo = 6,
                Description = "Excavate shallow by hand trench in stiff clay for foundation not exceeding 1.50m deep.",
                Unit = "m3",
                NetCost = Math.Round(costPerM3, 0),
                OverheadValue = Math.Round(ohp.overheadVal, 2),
                ProfitValue = Math.Round(ohp.profitVal, 2),
                TotalCost = Math.Round(ohp.total, 0)
            };
        }
        private GroundworkItem ComputeItem7()
        {
            double rollerCost = GetLabourRate("Static steel wheeled roller - (2.7 to 6 tonnes)");
            double dieselPrice = GetLabourRate("Labourer")/8;
            double operatorCost = GetLabourRate("Heavy plant operator")*1.4;
            double banksmanCost = GetLabourRate("Semi skilled")*1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;
            double literPerDay = 150;
            double outputPerDay = 276;
            double fuelCost = dieselPrice * literPerDay;

            double totalPlantDay = rollerCost + fuelCost +
                (0.03 * fuelCost) + (operatorCost) + (2 * banksmanCost) + (2 * labourCost);

            double plantCost = totalPlantDay / outputPerDay;

            double fillingInM2 = 0.18;
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);
            return new GroundworkItem
            {
                ItemNo = 7,
                Description = "Level and compact sand to a maximum thickness of 150mm to a maximum compaction of 100% using static wheel roller (2 to 6 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2)
            };
        }
        private GroundworkItem ComputeItem8()
        {
            double rollerCost = GetLabourRate("Vibratory whelled roller (8 to 10 tons)");

            double dieselPrice = GetLabourRate("Labourer") / 8;
            double literPerDay = 250;
            double fuelCost = dieselPrice * literPerDay;

            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double outputPerDay = 183;
            double outputVolumePerDay = outputPerDay * .3;

            double totalPlantDay = rollerCost + fuelCost +
                (0.03 * fuelCost) + (operatorCost) + (2 * banksmanCost) + (2 * labourCost);

            double plantCost = totalPlantDay / outputPerDay;

            double fillingInM2 = 0.36;
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);
            return new GroundworkItem
            {
                ItemNo = 8,
                Description = "Level and compact sand to a maximum thickness of 300mm in two layers of 150mm to a maximum compaction of " +
                "100% using smooth wheel roller (8 to 10 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2)
            };
        }
        private GroundworkItem ComputeItem9()
        {
            double rollerCost = GetLabourRate("Vibratory whelled roller (8 to 10 tons)");

            double dieselPrice = GetLabourRate("Labourer") / 8;
            double literPerDay = 250;
            double fuelCost = dieselPrice * literPerDay;

            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double outputPerDay = 183;
            double outputVolumePerDay = outputPerDay * .3;

            double totalPlantDay = rollerCost + fuelCost +
                (0.03 * fuelCost) + (operatorCost) + (2 * banksmanCost) + (2 * labourCost);

            double plantCost = totalPlantDay / outputPerDay;

            double fillingInM2 = 0.54;
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);
            return new GroundworkItem
            {
                ItemNo = 9,
                Description = "Level and compact sand to a maximum thickness of 450mm layers of 150mm to a maximum compaction of 100% using smooth wheel roller (8 to 10 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2)
            };
        }
        private GroundworkItem ComputeItem10()
        {
            double rollerCost = GetLabourRate("Vibratory whelled roller (8 to 10 tons)");

            double dieselPrice = (GetLabourRate("Labourer") / 8)*1.4;
            double literPerDay = 250;
            double fuelCost = dieselPrice * literPerDay;

            double operatorCost = GetLabourRate("Heavy plant operator") * 1.4;
            double banksmanCost = GetLabourRate("Semi skilled") * 1.4;
            double labourCost = GetLabourRate("Labourer") * 1.4;

            double outputPerDay = 93;
            double outputVolumePerDay = outputPerDay * .6;

            double totalPlantDay = rollerCost + fuelCost +
                (0.03 * fuelCost) + (operatorCost) + (2 * banksmanCost) + (2 * labourCost);

            double plantCost = totalPlantDay / outputVolumePerDay;

            double fillingInM2 = 0.72;
            double fillingCostPerM3 = GetMaterialPrice("Filling Sand (Beach)");
            double fillingPerM2 = fillingInM2 * fillingCostPerM3;

            double totalCompact = plantCost + fillingPerM2;
            var ohp = ApplyOHP(totalCompact);
            return new GroundworkItem
            {
                ItemNo = 10,
                Description = "Level and compact sand to a maximum thickness of 600mm in layers of 150mm to a maximum compaction of 100% using smooth wheel roller (8 to 10 ton) capacity",
                Unit = "m2",
                NetCost = Math.Round(totalCompact, 2),
                OverheadValue = Math.Round(ohp.overheadVal, 0),
                ProfitValue = Math.Round(ohp.profitVal, 0),
                TotalCost = Math.Round(ohp.total, 2)
            };
        }

        private GroundworkItem ComputeItem11()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem12()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem13()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem14()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem15()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem16()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem17()
        {
            throw new NotImplementedException();
        }

        private GroundworkItem ComputeItem18()
        {
            throw new NotImplementedException();
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

        private double GetMaterialPrice(string name)
        {
            var found = _materialLib.MaterialLibrary
                .FirstOrDefault(m => m.MaterialName == name);
            return (double)(found?.MaterialPrice ?? 0);
        }

        private double GetLabourRate(string name)
        {
            var found = _labourLib.LabourLibrary
                .FirstOrDefault(l => l.LabourName == name);
            return (double)(found?.LabourPrice ?? 0);
        }

          
    }
}
