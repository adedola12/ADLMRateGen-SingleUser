using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
    public class MaterialLibraryViewModel : ViewModelBase
    {
		//      private const string JsonPath = "materials.json";
		//      private const string DefaultJson = @"Data\defaultMaterials.json";
		//private readonly JsonDataServices _json = new(JsonPath, DefaultJson);

		/* ---------- data source ---------------------------------------------------------- */

		private const string JsonPath = "materials.json";
		private const string DefaultJson = @"Data\defaultMaterials.json";

		// generic helper specialises on <MaterialModel>
		private readonly JsonDataServices<MaterialModel> _json =
			new(JsonPath, DefaultJson);

		// ───────── collection bound to the grid ─────────
		public ObservableCollection<MaterialModel> MaterialLibrary { get; }
			= new();       // ←‑‑ instanced immediately

		public ICollectionView MaterialCollectionView { get; set; }

		public ICommand SearchMaterialCommand { get; }
		public ICommand ClearDatabaseCommand { get; }
		public ICommand DeleteMaterialCommand { get; }
		public ICommand EditMaterialCommand { get; }
		public ICommand UpdatePricesCommand { get; }

		public event Action<MaterialModel> EditMaterialRequested;
		public event Action LibraryChanged;

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
			//_dataServices = new JsonDataServices("materials.json", "Data\\defaultMaterials.json");
			//_json = new JsonDataServices(JsonPath, DefaultJson);

			//AddNewCommand = new RelayCommand(_ => OpenNewMaterialDialog());

			//var stored = _json.LoadData<ObservableCollection<MaterialModel>>() ??
			//			new ObservableCollection<MaterialModel>();
			foreach (var m in _json.LoadData()) MaterialLibrary.Add(m);

			MaterialCollectionView = CollectionViewSource.GetDefaultView(MaterialLibrary);
			ApplyFilter();

			MaterialPriceViewModel.MaterialSaved += AddOrUpdateMaterial;


			MaterialCollectionView.Filter = _ => true;

			bool useMongo = false; //true to upload to DB
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
				MaterialLibraryService.Initialize(new MaterialJsonDataSource(JsonPath));
			}

			MaterialLibrary = new ObservableCollection<MaterialModel>(MaterialLibraryService.GetAllMaterials());
			MaterialCollectionView = CollectionViewSource.GetDefaultView(MaterialLibrary);


            
            _selectedMaterialCategory = "All";

            EditMaterialCommand = new DelegateCommand(o => EditMaterial(o));
            DeleteMaterialCommand = new DelegateCommand(o => DeleteMaterial(o));
            SearchMaterialCommand = new DelegateCommand(o => ApplyFilter());
            ClearDatabaseCommand = new DelegateCommand(o => ClearDatabase());
            UpdatePricesCommand = new DelegateCommand(_ => UpdatePricesFromMongo());

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
            _json.SaveData(MaterialLibrary);
            ApplyFilter();
        }

        private void DeleteMaterial(object parameter)
        {
            if (parameter is MaterialModel material)
            {
                MaterialLibrary.Remove(material);
                ReassignSerialNumbers();
                _json.SaveData(MaterialLibrary);
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



		/// <summary>Add new OR update existing material.</summary>
		//public void AddOrUpdateMaterial(MaterialModel mat)
		//{
		//	if (mat.SerialNumber == 0)
		//		mat.SerialNumber = MaterialLibrary.Count == 0
		//						 ? 1
		//						 : MaterialLibrary.Max(m => m.SerialNumber) + 1;

		//	var existing = MaterialLibrary.FirstOrDefault(m => m.SerialNumber == mat.SerialNumber);
		//	if (existing == null)
		//		MaterialLibrary.Add(mat);
		//	else
		//	{
		//		var idx = MaterialLibrary.IndexOf(existing);
		//		MaterialLibrary[idx] = mat;                       // refresh row
		//	}

		//	PersistAndRefresh();
		//}

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
				existing.MaterialPrice = mat.MaterialPrice;   // price is the only editable field
			}

			Persist();
		}



		private void Persist()
		{
			_json.SaveData(MaterialLibrary);   // 💾
			LibraryChanged?.Invoke();
			ApplyFilter();
		}

		private void UpdatePricesFromMongo()
        {
			// Prompt the user for confirmation.
			var result = MessageBox.Show(
				"Are you sure you want to override the existing prices with prices from ADLM servers?",
				"Confirm Price Update",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			// If the user cancels, exit the method.
			if (result != MessageBoxResult.Yes)
			{
				MessageBox.Show("Price update canceled.", "Canceled", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
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
                {
                    localItem.MaterialPrice = matchingMongoItem.MaterialPrice;
				}
			}

            _json.SaveData(MaterialLibrary);

			LibraryChanged?.Invoke();
			ApplyFilter();

			MessageBox.Show("Prices updated from ADLM Servers.");


		}

		/* stub you can later replace with the real dialog */
		private void OpenNewMaterialDialog()
			=> MessageBox.Show("TODO: open *Add Material* dialog");

	}
}
