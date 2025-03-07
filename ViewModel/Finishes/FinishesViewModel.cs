using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Policy;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.ConcreteWork;

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
		public ObservableCollection<FinishesItem> FinishesItems { get; set; } =
			new ObservableCollection<FinishesItem>();
		public ICollectionView FinishesCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					FinishesCollectionView.Refresh();
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
		public FinishesViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourLib, BlockworkViewModel blockworkVM)
		{
			_helper = new GetItemsFromDB(matLib, labourLib);
			_blockworkViewModel = blockworkVM;
			matLib.LibraryChanged += OnLibraryChange;
			labourLib.LibraryChanged += OnLibraryChange;

			BuildFinishesItem();

			FinishesCollectionView = CollectionViewSource.GetDefaultView(FinishesItems);
			FinishesCollectionView.Filter = FilterFinishesItem;

			RecomputeCommand = new DelegateCommand(o => RecomputeAll());
			ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
		}

		#region Function Method
		private void ShowDetails(object o)
		{
			if(o is FinishesItem item)
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
			if(obj is FinishesItem item)
			{
				if (string.IsNullOrEmpty(SearchTerm))
				{
					return true;
				}
				return item.Description.IndexOf(SearchTerm,StringComparison.OrdinalIgnoreCase) >= 0;
			}

			return false;
		}
		private void RecomputeAll()
		{
			FinishesItems.Clear();
			BuildFinishesItem();
		}
		private void OnLibraryChange()
		{
			RecomputeAll();
		}
		private void BuildFinishesItem()
		{
			Func<FinishesItem>[] computeMethods =
			{
				ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,ComputeItem7,
				ComputeItem8,ComputeItem9,
				//ComputeItem10,ComputeItem11, ComputeItem12,ComputeItem13,
				//ComputeItem14,ComputeItem15,ComputeItem16,ComputeItem17,ComputeItem18, ComputeItem19,ComputeItem20,ComputeItem21
			};

			foreach(var compute in computeMethods)
			{
				FinishesItems.Add(compute());
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
		public double GetNetValue(Func<FinishesItem> computeItemFunc)
		{
			var item = computeItemFunc();
			return item.NetCost;
		}

		public double GetBlockworkNetValue(Func<BlockworkItem> computeFunc)
		{
			return _blockworkViewModel.GetNetValue(computeFunc);
		}

		#endregion

		#region Compute Methods

		private FinishesItem ComputeItem1()
		{
			double mortarCost = GetBlockworkNetValue(_blockworkViewModel.ComputeItem2) *0.012;
			double mortarWastePer = 5;
			double mortarWaste = mortarCost * (mortarWastePer / 100);

			//lABOUR COST
			double headmanCost = (GetLabourRate("Headman") ) * 1.4;
			double masonCost = (GetLabourRate("Skilled/Artisan") ) * 1.4;
			double labourCost = (GetLabourRate("Labourer") ) * 1.4;

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
			double unloadingBags = (GetLabourRate("Labourer")/8)*1.4 ;

			double whiteCementQty = 28;
			double whiteChippingQty =70;
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
			double firstPolishCostPer20SqmQty = Math.Round(4.0 / 20.0,2);
			double firstPolishGrindingMachine = Math.Round((5.0 / 60.0),2);
			double firstPolishTerrazoMan = Math.Round((5.0 / 60.0),2);
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
			double secondPolishCostPer20SqmQty = Math.Round(4.0 / 20.0,2);
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

			double totalSecondPolish = secondPolishCostPer20SqmRate + secondPolishGrindingRate + secondPolishTerrazoRate + secondPolishTilingRate;

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

			double totalFinalPolish = finalPolishCostPer20SqmRate + finalPolishGrindingRate + finalPolishTerrazoRate + finalPolishTilingRate;

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

		//private FinishesItem ComputeItem10()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem11()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem12()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem13()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem14()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem15()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem16()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem17()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem18()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem19()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem20()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem21()
		//{
		//	throw new NotImplementedException();
		//}






		#endregion
	}
}
