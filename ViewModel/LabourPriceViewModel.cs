using System.Collections.ObjectModel;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class LabourPriceViewModel: ViewModelBase
    {
        private string _labourName;
        private decimal _labourPrice;
        private string _labourUnit;
        private string _newLabourCategory;
        private LabourModel? _editingLabour;

        public event Action<LabourModel> LabourSaved;
        public event Action<bool, string?>? BusyChanged;


        public string LabourName
        {
            get => _labourName;
            set
            {
                if (_labourName != value)
                {
                    _labourName = value;
                    RaisePropertyChanged();
                }
            }
        }
        public decimal LabourPrice
        {
            get => _labourPrice;
            set
            {
                if (_labourPrice != value)
                {
                    _labourPrice = value;
                    RaisePropertyChanged();
                }
            }
        }
        public string LabourUnit
        {
            get => _labourUnit;
            set
            {
                if (_labourUnit != value)
                {
                    _labourUnit = value;
                    RaisePropertyChanged();
                }
            }
        }
        public string NewLabourCategory
        {
            get => _newLabourCategory;
            set
            {
                if (_newLabourCategory != value)
                {
                    _newLabourCategory = value;
                    RaisePropertyChanged();
                }
            }
        }
        public LabourModel? EditingLabour
        {
            get => _editingLabour;
            set
            {
                if (_editingLabour != value)
                {
                    _editingLabour = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsEditing));
                    ((DelegateCommand)SaveLabourCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)UpdateLabourCommand).RaiseCanExecuteChanged();
                }
            }
        }
        public bool IsEditing => EditingLabour != null;

        public ICommand SaveLabourCommand { get; }
        public ICommand UpdateLabourCommand { get; }

        public LabourPriceViewModel()
        {
            _newLabourCategory = string.Empty;
            EditingLabour = null;
            SaveLabourCommand = new DelegateCommand(o => SaveLabour(), o => !IsEditing);
            UpdateLabourCommand = new DelegateCommand(o => UpdateLabour(), o => IsEditing);
        }

        private void SaveLabour()
        {
           if(!string.IsNullOrEmpty(LabourName))
            {
                string categoryToUse = !string.IsNullOrEmpty(NewLabourCategory)? NewLabourCategory : string.Empty;
                var newLabour = new LabourModel
                {
                    SerialNumber = 0,
                    LabourName = this.LabourName,
                    LabourUnit = this.LabourUnit,
                    LabourPrice = this.LabourPrice,
                    LabourCategory = categoryToUse,
                };
                LabourSaved?.Invoke(newLabour);
                ClearInputFields();
            }
        }

        private void UpdateLabour()
        {
           if(EditingLabour != null)
            {
                //EditingLabour.LabourName = LabourName;
                EditingLabour.LabourUnit = LabourUnit;
                EditingLabour.LabourPrice = LabourPrice;

                string categoryToUse = !string.IsNullOrEmpty(NewLabourCategory) 
                    ? NewLabourCategory : EditingLabour.LabourCategory;
                EditingLabour.LabourCategory = categoryToUse;

                LabourSaved?.Invoke(EditingLabour);
                ClearInputFields();
                EditingLabour = null;
            }
        }
        private void ClearInputFields()
        {
            LabourName = string.Empty;
            LabourUnit = string.Empty;
            LabourPrice = 0;
            NewLabourCategory = string.Empty;
        }
    }
}
