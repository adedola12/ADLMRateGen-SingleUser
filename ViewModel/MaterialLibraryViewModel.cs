using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class MaterialLibraryViewModel : ViewModelBase
    {
        private readonly JsonDataServices _dataServices;
        private readonly string _filePath = "materials.json";
        private readonly string _defaultFilePath = "Data\\defaultMaterials.json";

        public ObservableCollection<MaterialModel> MaterialLibrary { get; set; }
        public ICollectionView MaterialCollectionView { get; set; }
        public ObservableCollection<string> MaterialCategory { get; set; }
        private string _selectedMaterialCategory;
        public string SelectedMaterialCategory
        {
            get => _selectedMaterialCategory;
            set
            {
                if (_selectedMaterialCategory != value)
                {
                    _selectedMaterialCategory = value;
                    RaisePropertyChanged();
                }
            }
        }



        public ICommand SearchMaterialCommand { get; }
        public ICommand ClearDatabaseCommand { get; }
        public ICommand DeleteMaterialCommand { get; }
        public ICommand EditMaterialCommand { get; }

        // Raised when the user clicks Edit on an item.
        public event Action<MaterialModel>? EditMaterialRequested;
        public event Action LibraryChanged;

        public MaterialLibraryViewModel()
        {
            //_dataServices = new JsonDataServices("materials.json", "Data\\defaultMaterials.json");
            _dataServices = new JsonDataServices(_filePath, _defaultFilePath);

            MaterialLibrary = _dataServices.LoadData<ObservableCollection<MaterialModel>>()
                              ?? new ObservableCollection<MaterialModel>();
            MaterialCollectionView = CollectionViewSource.GetDefaultView(MaterialLibrary);
            MaterialCategory = new ObservableCollection<string> { "All", "Ground Work", "Concrete Work", "FormWork" };
            _selectedMaterialCategory = "All";

            SearchMaterialCommand = new DelegateCommand(o => ApplyFilter());
            ClearDatabaseCommand = new DelegateCommand(o => ClearDatabase());
            DeleteMaterialCommand = new DelegateCommand(o => DeleteMaterial(o));
            EditMaterialCommand = new DelegateCommand(o => EditMaterial(o));
        }


        private void ApplyFilter()
        {
            if (MaterialCollectionView != null)
            {
                if (SelectedMaterialCategory == "All" || string.IsNullOrEmpty(SelectedMaterialCategory))
                    MaterialCollectionView.Filter = null;
                else
                    MaterialCollectionView.Filter = o =>
                    {
                        var material = o as MaterialModel;
                        return material != null && material.MaterialCategory == SelectedMaterialCategory;
                    };
                MaterialCollectionView.Refresh();
            }
        }

        private void ClearDatabase()
        {
            MaterialLibrary.Clear();
            _dataServices.SaveData(MaterialLibrary);
            ApplyFilter();
        }

        private void DeleteMaterial(object parameter)
        {
            if (parameter is MaterialModel material)
            {
                MaterialLibrary.Remove(material);
                ReassignSerialNumbers();
                _dataServices.SaveData(MaterialLibrary);
                LibraryChanged?.Invoke();
                ApplyFilter();
            }
        }

        private void EditMaterial(object parameter)
        {
            if (parameter is MaterialModel material)
            {
                // Raise event so that the parent can load this material for editing.
                EditMaterialRequested?.Invoke(material);
            }
        }

        private void ReassignSerialNumbers()
        {
            int serial = 1;
            foreach (var material in MaterialLibrary)
            {
                material.SerialNumber = serial++;
            }
        }

        // Called when a new material is added or an update is made.
        public void AddOrUpdateMaterial(MaterialModel material)
        {
            if (material.SerialNumber == 0)
            {
                int newSerial = MaterialLibrary.Count > 0 ? MaterialLibrary[^1].SerialNumber + 1 : 1;
                material.SerialNumber = newSerial;
                MaterialLibrary.Add(material);
            }
            // For updates, the item is already in the collection (its properties have been updated).
            _dataServices.SaveData(MaterialLibrary);
            LibraryChanged?.Invoke();
            ApplyFilter();
        }
    }
}
