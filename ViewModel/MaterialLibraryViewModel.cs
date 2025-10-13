using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;                    // ← needed for LINQ
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class MaterialLibraryViewModel : ViewModelBase
    {
        /* ---------- data source (AppData file) ---------- */
        private readonly MaterialJsonDataSource _ds = new(AppPaths.MaterialLibraryFile);

        // bound to the grid
        public ObservableCollection<MaterialModel> MaterialLibrary { get; } = new();

        public ICollectionView MaterialCollectionView { get; private set; }

        public ICommand SearchMaterialCommand { get; }
        public ICommand ClearDatabaseCommand { get; }
        public ICommand DeleteMaterialCommand { get; }
        public ICommand EditMaterialCommand { get; }
        public ICommand UpdatePricesCommand { get; }

        public event Action<MaterialModel> EditMaterialRequested;
        public event Action LibraryChanged;

        public double PriceNgnToCurrent(double baseNgn) => baseNgn * CurrencyService.Instance.Rate;

        public double GetMaterialPrice(string name)
        {
            var mat = MaterialLibraryService
                        .GetAllMaterials()
                        .FirstOrDefault(m => m.MaterialName == name);

            return mat == null ? 0 : (double)mat.MaterialPrice * CurrencyService.Instance.Rate;
        }

        public ICommand AddNewCommand { get; }

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
                    ApplyFilter();
                }
            }
        }

        private string _searchTerm = string.Empty;
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (_searchTerm != value)
                {
                    _searchTerm = value;
                    RaisePropertyChanged();
                    ApplyFilter();
                }
            }
        }

        public void RequestEdit(MaterialModel toEdit) => EditMaterialRequested?.Invoke(toEdit);

        public MaterialLibraryViewModel()
        {
            // Load from AppData file and point the shared service at the SAME DS
            foreach (var m in _ds.LoadMaterials()) MaterialLibrary.Add(m);
            MaterialLibraryService.Initialize(_ds);

            MaterialCollectionView = CollectionViewSource.GetDefaultView(MaterialLibrary);
            ApplyFilter();

            MaterialPriceViewModel.MaterialSaved += AddOrUpdateMaterial;
            MaterialCollectionView.Filter = _ => true;

            // If you later decide to use Mongo, keep both service and local collection in sync
            bool useMongo = false;
            if (useMongo)
            {
                var mongoDataSource = new MaterialMongoDataSource(
                    "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
                    "ADLMRateDB",
                    "Materials"
                );

                MaterialLibraryService.Initialize(mongoDataSource);

                var materialsFromMongo = MaterialLibraryService.GetAllMaterials().ToList();
                if (!materialsFromMongo.Any())
                {
                    BulkUploadUtility.BulkUploadJsonToMongo(
                        jsonFilePath: "Data\\defaultMaterials.json",
                        connectionString: "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
                        databaseName: "ADLMRateDB",
                        collectionName: "Materials"
                    );
                    MaterialLibraryService.Initialize(mongoDataSource);
                    materialsFromMongo = MaterialLibraryService.GetAllMaterials().ToList();
                }

                // refresh local grid from the service
                MaterialLibrary.Clear();
                foreach (var m in materialsFromMongo) MaterialLibrary.Add(m);
            }
            else
            {
                // already initialized with _ds; make sure local collection mirrors service
                MaterialLibrary.Clear();
                foreach (var m in MaterialLibraryService.GetAllMaterials()) MaterialLibrary.Add(m);
            }

            _selectedMaterialCategory = "All";

            EditMaterialCommand = new DelegateCommand(o => EditMaterial(o));
            DeleteMaterialCommand = new DelegateCommand(o => DeleteMaterial(o));
            SearchMaterialCommand = new DelegateCommand(o => ApplyFilter());
            ClearDatabaseCommand = new DelegateCommand(o => ClearDatabase());
            UpdatePricesCommand = new DelegateCommand(_ => UpdatePricesFromMongo());

            MaterialCategory = new ObservableCollection<string>
            {
                "All", "Cement Based Products", "Earthwork And Filling Materials", "Crushed Rock Products", "Terrazzo Products",
                "Mild Steel Bar Reinforcement", "High Tensile Steel Bar Reinforcement", "Mesh Reinforcement to B.S. 4483",
                "Timber - Softwood", "Timber - Hardwood", "Plywood - White", "Plywood - Brown", "Particle Board",
                "Plywood - Veneer", "Timber Others", "Glasswork - Louver Blade-Plain", "Glasswork - Louver Blade-Obscured",
                "Glasswork - Nacco Louver Carrier", "Glasswork - Sheet Glass 3mm", "Glasswork - Sheet Glass 4mm",
                "Glasswork - Sheet Glass 5mm", "Finishes - Ceramic Floor Tiles", "Finishes - Ceramic Wall Tiles",
                "Bituminous Products", "Fuels", "Structural Steel Plates", "Structural Steel",
                "Asa Ceilings Limited - Ceiling Boards", "Luxalon Ceilings", "Efisol Mineral Ceilings",
                "Nigerite Limited - Ceilings", "PVC Floor Tiles", "Longspan Aluminium Roofing Sheet",
                "Nigerite Products - SLW Asbestos", "Nigerite Products - Super Seven Asbestos",
                "Nails And Screws And Other Accessories", "Roof Felting", "Zinc Roofing Sheet",
                "Aluminium Doors And Windows - Natural Anodised (Plain Glazing)",
                "Aluminium Doors And Windows - Natural Anodised (Mylar Film Glazing)",
                "Aluminium Doors And Windows - Bullet Proof Glazing",
                "Aluminium Doors And Windows - Entrance Doors (Clear Sheet Glazing)",
                "Aluminium Doors And Windows - Entrance Doors (Bullet Proof)",
                "Aluminium Doors And Windows - Entrance Doors (Georgian Wired)",
                "Aluminium Doors And Windows - Entrance Doors (Georgian Wired, Mylar)",
                "Aluminium Doors And Windows - Composite (Clear Glazing)",
                "Aluminium Doors And Windows - Steel Doors (Vandal Proof)",
                "Aluminium Doors And Windows - Steel Doors (Bullet Proof)",
                "Insulated Wall Panels", "Curtain Wall",
                "Timber Doors", "Casement Window", "Paints - Emulsion", "Paints - Gloss Oil",
                "Paints - Chlorinated", "Paints - Peacock", "Paints - Road", "Paints - Wood",
                "AMERON PAINTS", "AMERON PAINTS - Finish Coating", "AMERON PAINTS - Anti-Fouling",
                "AMERON PAINTS - Degreaser", "AMERON PAINTS - Etching", "AMERON PAINTS - Cleaners",
                "AMERON PAINTS - Thinners", "AMERON PAINTS - Starter Liquid", "AMERON PAINTS - Solvent Free Epoxy",
                "CARBOLINE PAINTS", "PORTLAND PAINTS"
            };

            // refresh the grid on currency change
            CurrencyService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CurrencyService.Rate))
                    MaterialCollectionView.Refresh();
            };
        }

        private void ApplyFilter()
        {
            if (MaterialCollectionView == null) return;

            MaterialCollectionView.Filter = o =>
            {
                if (o is not MaterialModel material) return false;

                bool matchesCategory = SelectedMaterialCategory == "All" ||
                                       string.IsNullOrEmpty(SelectedMaterialCategory) ||
                                       material.MaterialCategory == SelectedMaterialCategory;

                bool matchesText = string.IsNullOrEmpty(SearchTerm) ||
                                   (!string.IsNullOrEmpty(material.MaterialName) &&
                                    material.MaterialName.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0);

                return matchesCategory && matchesText;
            };

            MaterialCollectionView.Refresh();
        }

        private void ClearDatabase()
        {
            MaterialLibrary.Clear();
            _ds.SaveMaterials(MaterialLibrary);
            MaterialLibraryService.Initialize(_ds); // refresh service cache & raise LibraryChanged
            ApplyFilter();
        }

        private void DeleteMaterial(object parameter)
        {
            if (parameter is MaterialModel material)
            {
                MaterialLibrary.Remove(material);
                ReassignSerialNumbers();
                _ds.SaveMaterials(MaterialLibrary);
                MaterialLibraryService.Initialize(_ds);
                LibraryChanged?.Invoke();
                ApplyFilter();
            }
        }

        private void EditMaterial(object parameter)
        {
            if (parameter is MaterialModel material)
                EditMaterialRequested?.Invoke(material);
        }

        private void ReassignSerialNumbers()
        {
            int serial = 1;
            foreach (var material in MaterialLibrary)
                material.SerialNumber = serial++;
        }

        public void AddOrUpdateMaterial(MaterialModel mat)
        {
            var existing = MaterialLibrary.FirstOrDefault(m => m.SerialNumber == mat.SerialNumber);

            if (existing == null)
            {
                mat.SerialNumber = MaterialLibrary.Count == 0
                    ? 1
                    : MaterialLibrary.Max(m => m.SerialNumber) + 1;
                MaterialLibrary.Add(mat);



            }
            else
            {
                existing.MaterialPrice = mat.MaterialPrice; // editable field
            }

            Persist();
        }

        private void Persist()
        {
            _ds.SaveMaterials(MaterialLibrary);
            MaterialLibraryService.Initialize(_ds);
            LibraryChanged?.Invoke();
            ApplyFilter();
        }

        private void UpdatePricesFromMongo()
        {
            var result = MessageBox.Show(
                "Are you sure you want to override the existing prices with prices from ADLM servers?",
                "Confirm Price Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                MessageBox.Show("Price update canceled.", "Canceled",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var mongoDataSource = new MaterialMongoDataSource(
                    "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
                    "ADLMRateDB",
                    "Materials"
                );
                var mongoMaterials = mongoDataSource.LoadMaterials().ToList();

                foreach (var localItem in MaterialLibrary)
                {
                    var matchingMongoItem = mongoMaterials.FirstOrDefault(m =>
                        m.MaterialName.Equals(localItem.MaterialName, StringComparison.OrdinalIgnoreCase));

                    if (matchingMongoItem != null)
                        localItem.MaterialPrice = matchingMongoItem.MaterialPrice;
                }

                _ds.SaveMaterials(MaterialLibrary);
                MaterialLibraryService.Initialize(_ds);
                LibraryChanged?.Invoke();
                ApplyFilter();

                MessageBox.Show("Prices updated from ADLM Servers.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to ADLM servers: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenNewMaterialDialog()
            => MessageBox.Show("TODO: open *Add Material* dialog");
    }
}
