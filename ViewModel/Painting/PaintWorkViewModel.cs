using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.SteelWork;

namespace ADLMRateGen.ViewModel.Painting
{
    public class PaintWorkViewModel: ViewModelBase
    {
        private readonly GetItemsFromDB _helper;
		private readonly SteelWorkViewModel _steelWorkViewModel;

		private double _overheadPercent = 10.0;
        private double _profitPercent = 25.0;
        private string _searchTerm = string.Empty;
        private object _selectedDetail;
		// ─── Sorting / filtering helpers ──────────────────────────────────────────────
		private bool _isNetCostFilterOn = false;          // toggled by “Filter ⌄”
		private SortState _currentSort = SortState.None;  // cycles in “Sort by ⌄”

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
		public ICommand FilterCommand { get; }   // NEW
		public ICommand SortCommand { get; }   // NEW
		public ICommand AddCustomRateCommand { get; }           // ❶ NEW

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
			FilterCommand = new DelegateCommand(_ => ToggleNetCostFilter());
			SortCommand = new DelegateCommand(_ => CycleSort());

			AddCustomRateCommand = new DelegateCommand(_ => OpenCustomRateEntry());
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
		// ────── FILTER – order by Net Cost (low → high) ──────
		private void ToggleNetCostFilter()
		{
			_isNetCostFilterOn = !_isNetCostFilterOn;

			PaintWorkCollectionView.SortDescriptions.Clear();

			if (_isNetCostFilterOn)
				PaintWorkCollectionView.SortDescriptions.Add(
					new SortDescription(nameof(PaintWorkItem.NetCost),
										ListSortDirection.Ascending));
		}

		// ────── SORT – cycle → None ▪ Overhead ▪ Total Cost ──────
		private void CycleSort()
		{
			// next state
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
						new SortDescription(nameof(PaintWorkItem.OverheadValue),
											ListSortDirection.Ascending));
					break;

				case SortState.TotalCost:
					PaintWorkCollectionView.SortDescriptions.Add(
						new SortDescription(nameof(PaintWorkItem.TotalCost),
											ListSortDirection.Ascending));
					break;

				case SortState.None:
				default:
					// back to the order in the underlying ObservableCollection
					break;
			}
		}

		private void OpenCustomRateEntry()
		{
			// create the entry view + its view‑model (DI / service‑locator would
			// be nicer, but a direct new‑up works fine here)
			var view = new CustomRateEntryView();
			view.DataContext = new CustomRateEntryViewModel();

			/* optional: close the popup when the entry VM tells us it's done
			   (expose bool IsSaved / event Saved in the entry‑VM if you like) */
			// ((CustomRateEntryViewModel)view.DataContext).Saved += () => SelectedDetail = null;

			SelectedDetail = view;         // GroundWorkView listens to this
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
		public double GetSteelNetValue(Func<SteelworkItem> computeFunc)
		{
			return _steelWorkViewModel.GetSteelNetValue(computeFunc);
		}

		public double GetNetValue(Func<PaintWorkItem> computeItemFunc)
        {
            var item = computeItemFunc();
            return item.NetCost;
        }
        private void BuildPaintworkItem()
        {
            Func<PaintWorkItem>[] computeMethods =
            {
                ComputeItem1, ComputeItem2, ComputeItem3,ComputeItem4,ComputeItem5,
                ComputeItem6,ComputeItem7,
                ComputeItem8,
				ComputeItem9,
				//ComputeItem10,ComputeItem11,ComputeItem12
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

			//Base coat Application
			double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
			double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double primerCost = GetMaterialPrice("Inorganic Zinc Silicate (Dimetcote 6, Redish Grey)");

			double sprayingMachineQty = 0.10;
			double sprayingLabouurQty = 0.10;
			double primerQty = 0.11;
			double primerWastePer = 5;

			double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
			double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
			double primerRate = primerCost * primerQty;
			double primerWaste = primerRate * (primerWastePer / 100);

			double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

			//First coat Application
			double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
			double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
            double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)") + GetMaterialPrice("Amercoat 8");

			double undercoatMachineQty = 0.10;
			double undercoatLabouurQty = 0.10;
			double epoxyQty = 0.11;
			double epoxyWastePer = 5;

			double undercoatMachineRate = undercoatMachineCost * undercoatMachineQty;
			double undercoatLabourRate = undercoatLabourCost * undercoatLabouurQty;
			double epoxyRate = epoxyCost * epoxyQty;
			double epoxyWaste = epoxyRate * (epoxyWastePer / 100);

			double totalUnderCoat = undercoatMachineRate + undercoatLabourRate + epoxyRate + epoxyWaste;

			//Second Coat Application
			double finishcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
			double finishcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double enamelCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)") ;

			double finishcoatMachineQty = 0.10;
			double finishcoatLabouurQty = 0.10;
			double enamelQty = 0.14;
			double enamelWastePer = 5;

			double finishcoatMachineRate = finishcoatMachineCost * finishcoatMachineQty;
			double finishcoatLabourRate = finishcoatLabourCost * finishcoatLabouurQty;
			double enamelRate = enamelCost * enamelQty;
			double enamelWaste = enamelRate * (enamelWastePer / 100);

			double totalFinishCoat = finishcoatMachineRate + finishcoatLabourRate + enamelRate + enamelWaste;

			//Top Coat Application
			double topcoatMachineCost = (GetLabourRate("Spraying machine") / 8);
			double topcoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double beigeCost = GetMaterialPrice("Mobil Beige Epoxy Enamel") / 4;

			double topcoatMachineQty = 0.10;
			double topcoatLabouurQty = 0.10;
			double beigeQty = 0.06;
			double beigeWastePer = 5;

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

			//Base coat Application
			double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
			double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double primerCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)");

			double sprayingMachineQty = 0.10;
			double sprayingLabouurQty = 0.10;
			double primerQty = 0.52;
			double primerWastePer = 5;

			double sprayingMachineRate = sprayingMachineCost * sprayingMachineQty;
			double sprayingLabourRate = sprayingLabourCost * sprayingLabouurQty;
			double primerRate = primerCost * primerQty;
			double primerWaste = primerRate * (primerWastePer / 100);

			double totalPrimer = sprayingMachineRate + sprayingLabourRate + primerRate + primerWaste;

			//First coat Application
			double undercoatMachineCost = (GetLabourRate("Spraying machine") / 8);
			double undercoatLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double epoxyCost = GetMaterialPrice("High Build Epoxy (Amercoat 78HBB, Black)") ;

			double undercoatMachineQty = 0.10;
			double undercoatLabouurQty = 0.10;
			double epoxyQty = 0.52;
			double epoxyWastePer = 5;

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

			double netCostPerSqm = totalBlastingPerDay + totalPrimer + totalUnderCoat ;

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
				Unit = "M2",
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
			double enamelCost = GetMaterialPrice("Ditto but OSHA S/Yellow (Finish Coating)") ;

			double finishcoatMachineQty = 0.10;
			double finishcoatLabouurQty = 0.10;
			double enamelQty = 0.09;
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
				Unit = "M2",
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
			double scrubLabourQty = 0.2;
			double scrubLabourRate = scrubLabour * scrubLabourQty;

			double latexCost = GetMaterialPrice("Multipurpose Polymaide Epoxy (Amercoat 385, Off White)");
			double finishLatexCost = GetMaterialPrice("Ditto but Mobil Beige F38");

			double latexQty = 0.4;
			double finishLatexQty = 0.4;

			double latexRate = latexCost * latexQty;
			double finishLatexRate = finishLatexCost * finishLatexQty;

			double waste = 10;

			double latexWaste = latexRate * (waste / 100);
			double finishLatexWaste = finishLatexRate * (waste / 100);

			double latexTotal = latexRate + latexWaste;
			double finishLatexTotal = finishLatexRate + finishLatexWaste;

			double painterCost = GetLabourRate("Skilled/Artisan");
			double painterQty = 1;
			double painterRate = painterCost * painterQty;

			double painterOutput = 18;
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
				Unit = "M2",
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
			double paintRemovalQty = 1;
			double paintRemovalRate = paintRemovalLabour * paintRemovalQty;

			//Primer Application
			double sprayingMachineCost = (GetLabourRate("Spraying machine") / 8);
			double sprayingLabourCost = (GetLabourRate("Skilled/Artisan") / 8) * 1.4;
			double primerCost = GetMaterialPrice("Epoxy Mastic (Amerlock 400AL, Aluminium Grey)");
			double thinnerCost = GetMaterialPrice("Amercoat 9HF");

			double sprayingMachineQty = 0.10;
			double sprayingLabouurQty = 0.15;
			double primerQty = 0.15;
			double primerWastePer = 15;
			double thinnerQty = primerQty * 0.1;

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

			double finishMachineQty = 0.10;
			double finishLabouurQty = 0.15;
			double polyurethanerQty = 0.09;
			double polyurethanerWastePer = 15;
			double thinnerFinishQty = polyurethanerQty * 0.1;

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
				Unit = "M2",
				NetCost = Math.Round(netCostPerSqm, 2),
				OverheadValue = Math.Round(ohp.overheadVal, 2),
				ProfitValue = Math.Round(ohp.profitVal, 2),
				TotalCost = Math.Round(ohp.total, 2),
				PaintingBreakdownLines = breakdown
			};
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
