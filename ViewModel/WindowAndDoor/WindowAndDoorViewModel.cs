using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.Finishes;

namespace ADLMRateGen.ViewModel.WindowAndDoor
{
    public class WindowAndDoorViewModel: ViewModelBase
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
		public ObservableCollection<WindowAndDoorItem> WindowAndDoorItems { get; set; } =
			new ObservableCollection<WindowAndDoorItem>();
		public ICollectionView WindowAndDoorCollectionView { get; private set; }
		public string SearchTerm
		{
			get => _searchTerm;
			set
			{
				if (_searchTerm != value)
				{
					_searchTerm = value;
					RaisePropertyChanged();
					WindowAndDoorCollectionView.Refresh();
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
		public WindowAndDoorViewModel(MaterialLibraryViewModel matLib, LabourLibraryViewModel labourlib)
		{
			_helper = new GetItemsFromDB(matLib, labourlib);

			matLib.LibraryChanged += OnLibraryChange;
			labourlib.LibraryChanged += OnLibraryChange;

			BuildWindowAndDoorItem();

			WindowAndDoorCollectionView = CollectionViewSource.GetDefaultView(WindowAndDoorItems);
			WindowAndDoorCollectionView.Filter = FilterWindowAndDoorItem;

			RecomputeCommand = new DelegateCommand(o => RecomputeAll());
			ShowDetailsCommand = new DelegateCommand(o => ShowDetails(o));
		}

		#region Function Method
		private void OnLibraryChange()
		{
			RecomputeAll();
		}
		private bool FilterWindowAndDoorItem(object obj)
		{
			if(obj is WindowAndDoorItem item)
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
			WindowAndDoorItems.Clear();
			BuildWindowAndDoorItem();
		}
		private void ShowDetails(object o)
		{
			if(o is WindowAndDoorItem item)
			{
				var detailedControl = new WindowAndDoorDetailControl();
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
		public double GetNetValue(Func<WindowAndDoorItem> computeItemFunc)
		{
			var item = computeItemFunc();
			return item.NetCost;
		}
		private void BuildWindowAndDoorItem()
		{
			Func<WindowAndDoorItem>[] computeMethods =
			{
				//ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,ComputeItem7,
				//ComputeItem8,ComputeItem9,ComputeItem10

			};

			foreach(var compute in computeMethods)
			{
				WindowAndDoorItems.Add(compute());
			}
		}
		#endregion

		#region Compute Method

		private WindowAndDoorItem ComputeItem1()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem2()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem3()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem4()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem5()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem6()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem7()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem8()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem9()
		{
			throw new NotImplementedException();
		}

		private WindowAndDoorItem ComputeItem10()
		{
			throw new NotImplementedException();
		} 
		#endregion
	}
}
