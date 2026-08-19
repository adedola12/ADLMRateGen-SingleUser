using ADLMRateGen.Helpers;
using System;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class LabourPriceViewModel : ViewModelBase
    {
        private string _labourName;
        private decimal _labourPrice;      // displayed in current currency
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
                    RaisePropertyChanged(nameof(Spec));
                    RaisePropertyChanged(nameof(HasSpec));
                }
            }
        }

        /// <summary>
        /// Reference specification and expected output for the item being edited,
        /// or null where none is recorded. Read-only: a day rate is a proxy for an
        /// output, and this is here so the person setting the rate can see the
        /// output it has to cover.
        /// </summary>
        public LabourSpec? Spec => LabourSpecs.Find(LabourName);

        public bool HasSpec => Spec != null;

        /// <summary>
        /// Price shown in the edit dialog – always in the CURRENT display currency.
        /// Converted to/from NGN on load/save.
        /// </summary>
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

        /// <summary>Current currency code for the price label hint.</summary>
        public string CurrencyLabel => CurrencyService.Instance.Code;

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

            // Keep CurrencyLabel up to date when currency changes
            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Code))
                    RaisePropertyChanged(nameof(CurrencyLabel));
            };
        }

        /// <summary>
        /// Call this when loading a labour item into the edit form.
        /// Converts the stored NGN price → current display currency.
        /// </summary>
        public void LoadForEdit(LabourModel labour)
        {
            EditingLabour = labour;
            LabourName = labour.LabourName;
            LabourUnit = labour.LabourUnit;
            LabourPrice = CurrencyService.Instance.FromNgn(labour.LabourPrice); // NGN → display
            NewLabourCategory = labour.LabourCategory ?? string.Empty;
        }

        private void SaveLabour()
        {
            if (!string.IsNullOrEmpty(LabourName))
            {
                // User entered price in current currency – convert back to NGN for storage
                var priceNgn = CurrencyService.Instance.ToNgn(LabourPrice);

                string categoryToUse = !string.IsNullOrEmpty(NewLabourCategory) ? NewLabourCategory : string.Empty;
                var newLabour = new LabourModel
                {
                    SerialNumber = 0,
                    LabourName = this.LabourName,
                    LabourUnit = this.LabourUnit,
                    LabourPrice = priceNgn,
                    LabourCategory = categoryToUse,
                };
                LabourSaved?.Invoke(newLabour);
                ClearInputFields();
            }
        }

        private void UpdateLabour()
        {
            if (EditingLabour != null)
            {
                EditingLabour.LabourUnit = LabourUnit;
                // User edited price in current currency – convert back to NGN for storage
                EditingLabour.LabourPrice = CurrencyService.Instance.ToNgn(LabourPrice);

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
