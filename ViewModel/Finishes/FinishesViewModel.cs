using System.Collections.ObjectModel;
using System.ComponentModel;
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
				//ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,
				//ComputeItem7,ComputeItem8,ComputeItem9,ComputeItem10,ComputeItem11, ComputeItem12,ComputeItem13,
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

		//#region Compute Methods

		//private FinishesItem ComputeItem1()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem2()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem3()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem4()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem5()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem6()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem7()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem8()
		//{
		//	throw new NotImplementedException();
		//}

		//private FinishesItem ComputeItem9()
		//{
		//	throw new NotImplementedException();
		//}

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






		//#endregion
	}
}
