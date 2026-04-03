using System;
using System.Windows.Input;

using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class MaterialPriceViewModel : ViewModelBase
    {
        private string? _materialName;
        private decimal _materialPrice;      // displayed in current currency
        private string? _materialUnit;
        private string _newMaterialCategory = string.Empty;
        private MaterialModel? _editingMaterial;

        // Event raised when a new material is saved or an existing one is updated.
        public static event Action<MaterialModel>? MaterialSaved;

        public string? MaterialName
        {
            get => _materialName;
            set
            {
                if (_materialName != value)
                {
                    _materialName = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Price shown in the edit dialog – always in the CURRENT display currency.
        /// Converted to/from NGN on load/save.
        /// </summary>
        public decimal MaterialPrice
        {
            get => _materialPrice;
            set
            {
                if (_materialPrice != value)
                {
                    _materialPrice = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>Current currency code for the price label hint.</summary>
        public string CurrencyLabel => CurrencyService.Instance.Code;

        public string? MaterialUnit
        {
            get => _materialUnit;
            set
            {
                if (_materialUnit != value)
                {
                    _materialUnit = value;
                    RaisePropertyChanged();
                }
            }
        }
        // This property holds a new category entered by the user.
        public string NewMaterialCategory
        {
            get => _newMaterialCategory;
            set
            {
                if (_newMaterialCategory != value)
                {
                    _newMaterialCategory = value;
                    RaisePropertyChanged();
                }
            }
        }
        public MaterialModel? EditingMaterial
        {
            get => _editingMaterial;
            set
            {
                if (_editingMaterial != value)
                {
                    _editingMaterial = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsEditing));
                    ((DelegateCommand)SaveMaterialCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)UpdateMaterialCommand).RaiseCanExecuteChanged();
                }
            }
        }
        public bool IsEditing => EditingMaterial != null;

        public ICommand SaveMaterialCommand { get; }
        public ICommand UpdateMaterialCommand { get; }

        public MaterialPriceViewModel()
        {
            SaveMaterialCommand = new DelegateCommand(o => SaveMaterial(), o => !IsEditing);
            UpdateMaterialCommand = new DelegateCommand(o => UpdateMaterial(), o => IsEditing);

            // Keep CurrencyLabel up to date when currency changes
            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrencyService.Code))
                    RaisePropertyChanged(nameof(CurrencyLabel));
            };
        }

        /// <summary>
        /// Call this when loading a material into the edit form.
        /// Converts the stored NGN price → current display currency.
        /// </summary>
        public void LoadForEdit(MaterialModel material)
        {
            EditingMaterial = material;
            MaterialName = material.MaterialName;
            MaterialUnit = material.MaterialUnit;
            MaterialPrice = CurrencyService.Instance.FromNgn(material.MaterialPrice); // NGN → display
            NewMaterialCategory = material.MaterialCategory ?? string.Empty;
        }

        /* ───────── add new ───────── */
        private void SaveMaterial()
        {
            if (string.IsNullOrWhiteSpace(MaterialName)) return;

            // User entered price in current currency – convert back to NGN for storage
            var priceNgn = CurrencyService.Instance.ToNgn(MaterialPrice);

            var mat = new MaterialModel
            {
                SerialNumber = 0,
                MaterialName = MaterialName,
                MaterialUnit = MaterialUnit,
                MaterialPrice = priceNgn,
                MaterialCategory = string.IsNullOrWhiteSpace(NewMaterialCategory)
                                 ? string.Empty
                                 : NewMaterialCategory
            };

            MaterialSaved?.Invoke(mat);
            ClearInputs();
        }

        /* ───────── update price only ───────── */
        private void UpdateMaterial()
        {
            if (EditingMaterial == null) return;

            // User edited price in current currency – convert back to NGN for storage
            EditingMaterial.MaterialPrice = CurrencyService.Instance.ToNgn(MaterialPrice);

            if (!string.IsNullOrWhiteSpace(NewMaterialCategory))
                EditingMaterial.MaterialCategory = NewMaterialCategory;

            MaterialSaved?.Invoke(EditingMaterial);
            ClearInputs();
            EditingMaterial = null;
        }

        private void ClearInputs()
        {
            MaterialName = string.Empty;
            MaterialUnit = string.Empty;
            MaterialPrice = 0;
            NewMaterialCategory = string.Empty;
        }
    }
}
