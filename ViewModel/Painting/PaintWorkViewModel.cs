using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.WindowAndDoor;

namespace ADLMRateGen.ViewModel.Painting
{
    public class PaintWorkViewModel: ViewModelBase
    {
        private readonly GetItemsFromDB _helper;

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
                if(_searchTerm != value)
                {
                    _searchTerm = value;
                    RaisePropertyChanged();
                    PaintWorkCollectionView.Refresh();
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

        public PaintWorkViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib)
        {
            _helper = new GetItemsFromDB(matLib, labourLib);
            matLib.LibraryChanged += OnLibraryChange;
            labourLib.LibraryChanged += OnLibraryChange;

            BuildPaintworkItem();

            PaintWorkCollectionView = CollectionViewSource.GetDefaultView(PaintWorkItems);
            PaintWorkCollectionView.Filter = FilterPaintworkItem;

            RecomputeCommand = new DelegateCommand(o => RecomputeAll());
            ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
        }

        #region Function Method
        private void OnLibraryChange()
        {
            RecomputeAll();
        }
        private bool FilterPaintworkItem(object obj)
        {
            if(obj is PaintWorkItem item)
            {
                if (string.IsNullOrEmpty(SearchTerm))
                {
                    return true;
                }
                return item.Description.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;

            }
            return false;
        }
        private void RecomputeAll()
        {
            PaintWorkItems.Clear();
            BuildPaintworkItem();
        }
        private void ShowDetails(object o)
        {
            if(o is PaintWorkItem item)
            {
                var detailedControl = new PaintworkDetailControl();
                detailedControl.DataContext = item;

                detailedControl.BackRequested += () =>
                {
                    SelectedDetail = null;
                };

                SelectedDetail = detailedControl;
            }
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
        public double GetNetValue(Func<PaintWorkItem> computeItemFunc)
        {
            var item = computeItemFunc();
            return item.NetCost;
        }
        private void BuildPaintworkItem()
        {
            Func<PaintWorkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3,
                ComputeItem4,
                //ComputeItem5,ComputeItem6,ComputeItem7,
                //ComputeItem8,ComputeItem9,ComputeItem10,ComputeItem11,ComputeItem12
            };

            foreach(var compute in computeMethods)
            {
                PaintWorkItems.Add(compute());
            }
        }
        #endregion

        #region COMPUTE METHOD
        private PaintWorkItem ComputeItem1()
        {
            //MATERIAL CLASS
            double puttyCost = GetMaterialPrice("Poly Filla 1.8Kg Pack.");
            double antiFungalCost = GetMaterialPrice("Pealux Anti Fungi/Algae Emulsion")/4;
            double texcoteCost = GetMaterialPrice("Peacotex Textured Finish (B/W)") /25;

            double puttyQty = 0.3;
            double antiFungalQty = 0.4;
            double texcoteQty = 1;

            double puttyRate = puttyCost * puttyQty;
            double antiFungalRate = antiFungalCost * antiFungalQty;
            double texcoteRate = texcoteCost * texcoteQty;

            double puttyWastePer = 2.5;
            double antiFungalWastePer = 10;
            double texcoatWastePer = 10;

            double puttyWaste = puttyRate * (puttyWastePer / 100);
            double antiFungalWaste = antiFungalRate *(antiFungalWastePer / 100);
            double texcoatWaste = texcoteRate * (texcoatWastePer / 100);

            double materialTotal = puttyRate + antiFungalRate + texcoteRate + 
                puttyWaste + antiFungalWaste + texcoatWaste;

            //LABOUR COST
			double painterCost = (GetLabourRate("Skilled/Artisan") ) * 1.4;
            double painterQty = 2;
            double painterRate = painterCost * painterQty;

            double painterOutput = 18;

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
                Unit = "M2",
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

            double puttyQty = 0.3;
            //double antiFungalQty = 0.4;
            double texcoteQty = 1;

            double puttyRate = puttyCost * puttyQty;
            //double antiFungalRate = antiFungalCost * antiFungalQty;
            double texcoteRate = texcoteCost * texcoteQty;

            double puttyWastePer = 2.5;
            //double antiFungalWastePer = 10;
            double texcoatWastePer = 10;

            double puttyWaste = puttyRate * (puttyWastePer / 100);
            //double antiFungalWaste = antiFungalRate * (antiFungalWastePer / 100);
            double texcoatWaste = texcoteRate * (texcoatWastePer / 100);

            double materialTotal = puttyRate  + texcoteRate +
                puttyWaste  + texcoatWaste;

            //LABOUR COST
            double painterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double painterQty = 2;
            double painterRate = painterCost * painterQty;

            double painterOutput = 30;

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
                Unit = "M2",
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

            double puttyQty = 0.3;
            double primingQty = 0.28;
            double emulsionQty = 0.42;

            double puttyRate = puttyCost * puttyQty;
            double primingRate = primingCost * primingQty;
            double emulsionRate = emulsionCost * emulsionQty;

            double puttyWastePer = 2.5;
            double primingWastePer = 10;
            double emulsionWastePer = 10;

            double puttyWaste = puttyRate * (puttyWastePer / 100);
            double primingWaste = primingRate * (primingWastePer / 100);
            double emulsionWaste = emulsionRate * (emulsionWastePer / 100);

            double materialTotal = puttyRate + primingRate + emulsionRate +
                puttyWaste + primingWaste + emulsionWaste;

            //LABOUR COST
            double painterCost = (GetLabourRate("Skilled/Artisan")) * 1.4;
            double painterQty = 2;
            double painterRate = painterCost * painterQty;

            double painterOutput = 20;

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
                Unit = "M2",
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

            double chemicalQty = 0.2;
            double chemicalLabourQty = 0.08;

            double chemicalRate = chemicalCost * chemicalQty;
            double chemicalLabourRate = chemicalLabourCost * chemicalLabourQty;
            double chemicalTotal = chemicalRate + chemicalLabourRate;

            //Blasting
            double compressorCost = GetLabourRate("Compressor")/8;
            double fuelCost = GetMaterialPrice("Diesel");
            double sandPotCost = GetLabourRate("Sand Pot for sand blasting")/8;
            double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
            double gritCost = GetMaterialPrice("Grit (for sand blasting)");

            double compressorQty = 0.025;
            double fuelQty = 45;
            double sandPotQty = 0.025;
            double respiratoryQty = 0.025;
            double gritQty = 0.15;
            double oilPer = 3;

            double compressorRate = compressorCost * compressorQty;
            double fuelRate = fuelCost * fuelQty;
            double sandPotRate = sandPotCost * sandPotQty;
            double respiratoryRate = respiratoryCost * respiratoryQty;
            double gritRate = gritCost * gritQty;
            double oilRate = fuelRate * (oilPer / 100);

            double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
            double blastingLabourCost = GetLabourRate("Labourer") * 1.4;

            double blastingOperatorQty = 1;
            double blastingLabouurQty = 2;

            double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
            double blastingLabourRate =  blastingLabourCost* blastingLabouurQty;
            double blastingLabour = blastingOperatorRate + blastingLabourRate;
			double blastingOutputDaily = 300;

			double blastingPerSqm = blastingLabour / blastingOutputDaily;

			double totalBlastingPerDay = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

            //Primer Application
            double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8) ;
            double sprayingLabourCost = (GetLabourRate("Skilled/Artisan")/8) *1.4;
            double primerCost = GetMaterialPrice("Zinc Rich Epoxy (Amercoat 64, Pale Red)");

            double sprayingMachineQty = 0.10;
            double sprayingLabouurQty = 0.1;
            double primerQty = 0.11;
            double primerWastePer = 5;

            double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
            double sprayingLabourRate = sprayingLabourCost* sprayingLabouurQty;
            double primerRate = primerCost* primerQty;
            double primerWaste = primerRate * (primerWastePer / 100);

            double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

			//Undercoat Application
			double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8) ;
			double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

			double undercoatMachineQty = 0.10;
			double undercoatLabouurQty = 0.10;
			double epoxyQty = 0.13;
			double epoxyWastePer = 5;

			double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
			double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
			double epoxyRate = epoxyCost * epoxyQty;
			double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

            double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

			//FinishCoat Application
			double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8) ;
			double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double enamelCost = GetMaterialPrice("Mobil Beige Epoxy Enamel")/4;

			double finishcoatMachineQty = 0.10;
			double finishcoatLabouurQty = 0.10;
			double enamelQty = 0.08;
			double enamelWastePer = 5;

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
				Unit = "M2",
				NetCost = Math.Round(netCostPerSqm, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				PaintingBreakdownLines = breakdown
			};
		}

        private PaintWorkItem ComputeItem5()
        {
			double chemicalCost = GetMaterialPrice("Oil and Grease Remover (Amercoat 57 OC)");
			double chemicalLabourCost = (GetLabourRate("Labourer") / 8) * 1.4;

			double chemicalQty = 0.2;
			double chemicalLabourQty = 0.08;

			double chemicalRate = chemicalCost * chemicalQty;
			double chemicalLabourRate = chemicalLabourCost * chemicalLabourQty;
			double chemicalTotal = chemicalRate + chemicalLabourRate;

			//Blasting
			double compressorCost = GetLabourRate("Compressor") / 8;
			double fuelCost = GetMaterialPrice("Diesel");
			double sandPotCost = GetLabourRate("Sand Pot for sand blasting") / 8;
			double respiratoryCost = GetLabourRate("Respiratory gear for sand blasting") / 8;
			double gritCost = GetMaterialPrice("Grit (for sand blasting)");

			double compressorQty = 0.025;
			double fuelQty = 45;
			double sandPotQty = 0.025;
			double respiratoryQty = 0.025;
			double gritQty = 0.15;
			double oilPer = 3;

			double compressorRate = compressorCost * compressorQty;
			double fuelRate = fuelCost * fuelQty;
			double sandPotRate = sandPotCost * sandPotQty;
			double respiratoryRate = respiratoryCost * respiratoryQty;
			double gritRate = gritCost * gritQty;
			double oilRate = fuelRate * (oilPer / 100);

			double blastingOperatorCost = GetLabourRate("Light plant operator") * 1.4;
			double blastingLabourCost = GetLabourRate("Labourer") * 1.4;

			double blastingOperatorQty = 1;
			double blastingLabouurQty = 2;

			double blastingOperatorRate = blastingOperatorCost * blastingOperatorQty;
			double blastingLabourRate = blastingLabourCost * blastingLabouurQty;
			double blastingLabour = blastingOperatorRate + blastingLabourRate;
			double blastingOutputDaily = 300;

			double blastingPerSqm = blastingLabour / blastingOutputDaily;

			double totalBlastingPerDay = compressorRate + fuelRate + sandPotRate + respiratoryRate + gritRate + oilRate + blastingPerSqm;

			//Primer Application
			double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
			double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double primerCost = GetMaterialPrice("Zinc Rich Epoxy (Amercoat 64, Pale Red)");

			double sprayingMachineQty = 0.10;
			double sprayingLabouurQty = 0.1;
			double primerQty = 0.11;
			double primerWastePer = 5;

			double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
			double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
			double primerRate = primerCost * primerQty;
			double primerWaste = primerRate * (primerWastePer / 100);

			double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

			//Undercoat Application
			double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
			double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

			double undercoatMachineQty = 0.10;
			double undercoatLabouurQty = 0.10;
			double epoxyQty = 0.13;
			double epoxyWastePer = 5;

			double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
			double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
			double epoxyRate = epoxyCost * epoxyQty;
			double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

			double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

			//FinishCoat Application
			double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
			double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double enamelCost = GetMaterialPrice("Mobil Beige Epoxy Enamel") / 4;

			double finishcoatMachineQty = 0.10;
			double finishcoatLabouurQty = 0.10;
			double enamelQty = 0.08;
			double enamelWastePer = 5;

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
				Unit = "M2",
				NetCost = Math.Round(netCostPerSqm, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				PaintingBreakdownLines = breakdown
			};
		}

        private PaintWorkItem ComputeItem6()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem7()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem8()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem9()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem10()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem11()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem12()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
