using System.Collections.ObjectModel;
using System.ComponentModel;
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
                //ComputeItem1,ComputeItem2,ComputeItem3,ComputeItem4,ComputeItem5,ComputeItem6,ComputeItem7,
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
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem2()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem3()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem4()
        {
            throw new NotImplementedException();
        }

        private PaintWorkItem ComputeItem5()
        {
            throw new NotImplementedException();
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
