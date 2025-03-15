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

            bool useMongo = true;
            if (useMongo)
            {
                var mongoDataSource = new MaterialMongoDataSource(
                    "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
                    "ADLMRateDB",
                    "Materials"
                    );
                MaterialLibraryService.Initialize(mongoDataSource);

                var materialsFromMongo = MaterialLibraryService.GetAllMaterials();
                if(materialsFromMongo == null || !materialsFromMongo.Any())
				{
					BulkUploadUtility.BulkUploadJsonToMongo(
                        jsonFilePath: "Data\\defaultMaterials.json",
                        connectionString: "mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
                        databaseName: "ADLMRateDB",
						collectionName: "Materials"
						);

                    MaterialLibraryService.Initialize(mongoDataSource);
				}
			} else
            {
				MaterialLibraryService.Initialize(new MaterialJsonDataSource(_defaultFilePath));
			}


			MaterialLibrary = new ObservableCollection<MaterialModel>(MaterialLibraryService.GetAllMaterials());
			MaterialCollectionView = CollectionViewSource.GetDefaultView(MaterialLibrary);


            MaterialCategory = new ObservableCollection<string> { "All", "Cement Based Products", "Earthwork And Filling Materials", "Crushed Rock Products", "Terrazzo Products", 
                "Mild Steel Bar Reinforcement", "High Tensile Steel Bar Reinforcement", "Mesh Reinforcement to B.S. 4483", "Timber - Softwood", "Timber - Hardwood", 
                "Plywood - White", "Plywood - Brown", "Particle Board", "Plywood - Veneer", "Timber Others", "Glasswork - Louver Blade-Plain", "Glasswork - Louver Blade-Obscured", 
                "Glasswork - Nacco Louver Carrier", "Glasswork - Sheet Glass 3mm", "Glasswork - Sheet Glass 4mm", "Glasswork - Sheet Glass 5mm", "Finishes - Ceramic Floor Tiles", 
                "Finishes - Ceramic Wall Tiles", "Bituminous Products", "Fuels", "Structural Steel Plates", "Structural Steel", "Asa Ceilings Limited - Ceiling Boards", 
                "Luxalon Ceilings", "Efisol Mineral Ceilings", "Nigerite Limited - Ceilings", "PVC Floor Tiles", "Longspan Aluminium Roofing Sheet", "Nigerite Products - SLW Asbestos",
                "Nigerite Products - Super Seven Asbestos", "Nails And Screws And Other Accessories", "Roof Felting", "Zinc Roofing Sheet", 
                "Aluminium Doors And Windows - Natural Anodised (Plain Glazing)", "Aluminium Doors And Windows - Natural Anodised (Mylar Film Glazing)", 
                "Aluminium Doors And Windows - Bullet Proof Glazing", "Aluminium Doors And Windows - Entrance Doors (Clear Sheet Glazing)", 
                "Aluminium Doors And Windows - Entrance Doors (Bullet Proof)", "Aluminium Doors And Windows - Entrance Doors (Georgian Wired)", 
                "Aluminium Doors And Windows - Entrance Doors (Georgian Wired, Mylar)", "Aluminium Doors And Windows - Composite (Clear Glazing)", 
                "Aluminium Doors And Windows - Steel Doors (Vandal Proof)", "Aluminium Doors And Windows - Steel Doors (Bullet Proof)", "Insulated Wall Panels", "Curtain Wall", 
                "Timber Doors", "Casement Window", "Paints - Emulsion", "Paints - Gloss Oil", "Paints - Chlorinated", "Paints - Peacock", "Paints - Road", "Paints - Wood", 
                "AMERON PAINTS", "AMERON PAINTS - Finish Coating", "AMERON PAINTS - Anti-Fouling", "AMERON PAINTS - Degreaser", "AMERON PAINTS - Etching", "AMERON PAINTS - Cleaners", 
                "AMERON PAINTS - Thinners", "AMERON PAINTS - Starter Liquid", "AMERON PAINTS - Solvent Free Epoxy", "CARBOLINE PAINTS", "PORTLAND PAINTS"
            };
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
				MaterialCollectionView.Filter = o =>
				{
					if (o is MaterialModel material)
					{
						// Filter by category if not "All"
						bool matchesCategory = SelectedMaterialCategory == "All" ||
											   string.IsNullOrEmpty(SelectedMaterialCategory) ||
											   material.MaterialCategory == SelectedMaterialCategory;
						// Filter by text if search term is provided.
						bool matchesText = string.IsNullOrEmpty(SearchTerm) ||
										   (!string.IsNullOrEmpty(material.MaterialName) &&
										   material.MaterialName.IndexOf(SearchTerm, System.StringComparison.OrdinalIgnoreCase) >= 0);
						return matchesCategory && matchesText;
					}
					return false;
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
