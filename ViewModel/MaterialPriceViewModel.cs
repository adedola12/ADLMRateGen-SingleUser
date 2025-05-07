using System;
using System.Windows.Input;

using ADLMRateGen.Command;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class MaterialPriceViewModel : ViewModelBase
    {
        private string? _materialName;
        private decimal _materialPrice;
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
            //_newMaterialCategory = string.Empty;
            //EditingMaterial = null;
            SaveMaterialCommand = new DelegateCommand(o => SaveMaterial(), o => !IsEditing);
            UpdateMaterialCommand = new DelegateCommand(o => UpdateMaterial(), o => IsEditing);
        }

		//private void SaveMaterial()
		//{
		//    if (!string.IsNullOrEmpty(MaterialName))
		//    {
		//        // Use NewMaterialCategory if provided; otherwise, leave category empty.
		//        string categoryToUse = !string.IsNullOrEmpty(NewMaterialCategory)
		//            ? NewMaterialCategory : string.Empty;
		//        // Create new material (serial number to be assigned by the library view model).
		//        var newMaterial = new MaterialModel
		//        {
		//            SerialNumber = 0,
		//            MaterialName = this.MaterialName,
		//            MaterialUnit = this.MaterialUnit,
		//            MaterialPrice = this.MaterialPrice,
		//            MaterialCategory = categoryToUse
		//        };
		//        MaterialSaved?.Invoke(newMaterial);
		//        ClearInputFields();
		//    }
		//}

		/* ───────── add new ───────── */
		private void SaveMaterial()
		{
			if (string.IsNullOrWhiteSpace(MaterialName)) return;

			var mat = new MaterialModel
			{
				SerialNumber = 0,
				MaterialName = MaterialName,
				MaterialUnit = MaterialUnit,
				MaterialPrice = MaterialPrice,
				MaterialCategory = string.IsNullOrWhiteSpace(NewMaterialCategory)
								 ? string.Empty
								 : NewMaterialCategory
			};

			MaterialSaved?.Invoke(mat);
			ClearInputs();
		}

		//private void UpdateMaterial()
		//{
		//    if (EditingMaterial != null)
		//    {
		//        EditingMaterial.MaterialName = MaterialName;
		//        EditingMaterial.MaterialUnit = MaterialUnit;
		//        EditingMaterial.MaterialPrice = MaterialPrice;
		//        string categoryToUse = !string.IsNullOrEmpty(NewMaterialCategory)
		//            ? NewMaterialCategory : EditingMaterial.MaterialCategory;
		//        EditingMaterial.MaterialCategory = categoryToUse;
		//        MaterialSaved?.Invoke(EditingMaterial);
		//        ClearInputFields();
		//        EditingMaterial = null;
		//    }
		//}

		/* ───────── update price only ───────── */
		private void UpdateMaterial()
		{
			if (EditingMaterial == null) return;

			EditingMaterial.MaterialPrice = MaterialPrice;            // price change only
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
