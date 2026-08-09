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
        public event Action<bool, string?>? BusyChanged;


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

            // Public builds read from the local library file and use API sync for cloud updates.
            MaterialLibrary.Clear();
            foreach (var m in MaterialLibraryService.GetAllMaterials()) MaterialLibrary.Add(m);

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
                "CARBOLINE PAINTS", "PORTLAND PAINTS",

                // Mechanical, electrical and plumbing, added in catalog 2026.08.
                // These carry INSTALLED rates, not bare material prices - the bills price
                // MEP as "supply, fix, connect & commission" and the rates were taken as
                // priced rather than split into material and labour. The category name
                // says so, so a rate built from these must not add a fixing labour line.
                "MEP - Cables & Wiring (supply & install)",
                "MEP - Earthing & Lightning Protection (supply & install)",
                "MEP - Cable Containment & Ducts (supply & install)",
                "MEP - Luminaires (supply & install)",
                "MEP - Switches & Socket Outlets (supply & install)",
                "MEP - Distribution & Switchgear (supply & install)",
                "MEP - Point Wiring (supply & install)",
                "MEP - Sanitary Fittings (supply & install)",
                "MEP - Air Conditioning & Ventilation (supply & install)",
                "MEP - Fire Protection (supply & install)",
                "MEP - Security & Detection (supply & install)"
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

        private async void DeleteMaterial(object parameter)
        {
            if (parameter is not MaterialModel material) return;

            // only user-added rows are deletable
            if (!ADLMRateGen.Services.UserRowChecker.IsUserMaterial(material.SerialNumber, material.MaterialName ?? ""))
            {
                MessageBox.Show("Only your own added materials can be deleted.\nMaster items cannot be removed.",
                    "Not allowed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // warn if used in custom rates
            int uses = ADLMRateGen.ViewModel.CustomRate.CustomRateUsage.CountMaterialUsage(material.MaterialName ?? "");
            if (uses > 0)
            {
                var r = MessageBox.Show(
                    $"{uses} custom rate item(s) use \"{material.MaterialName}\".\n" +
                    "If you proceed, they will be removed from those custom rates.\n\nProceed?",
                    "Used in Custom Rates", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (r != MessageBoxResult.Yes) return;

                // remove from custom rates to keep things consistent
                ADLMRateGen.ViewModel.CustomRate.CustomRateUsage.RemoveMaterialEverywhere(material.MaterialName ?? "");
            }

            // local remove
            MaterialLibrary.Remove(material);

            // keep local numbering tidy for UI
            ReassignSerialNumbers();
            _ds.SaveMaterials(MaterialLibrary);
            MaterialLibraryService.Initialize(_ds);
            LibraryChanged?.Invoke();
            ApplyFilter();

            // user feedback now
            MessageBox.Show("Material deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);


            // remove from server user library (best-effort)
            _ = UserLibrarySync.Instance.DeleteMaterialAsync(material.SerialNumber, material.MaterialName);
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

        public async void AddOrUpdateMaterial(MaterialModel mat)
        {
            var existing = MaterialLibrary.FirstOrDefault(m => m.SerialNumber == mat.SerialNumber);

            if (existing == null)
            {
                // keep numbering continuous (master + existing user items already in collection)
                mat.SerialNumber = MaterialLibrary.Count == 0
                    ? 1
                    : MaterialLibrary.Max(m => m.SerialNumber) + 1;

                MaterialLibrary.Add(mat);
                Persist();

                // mirror to server (best-effort)
                _ = UserLibrarySync.Instance.TryAddMaterialAsync(mat);
            }
            else
            {
                existing.MaterialPrice = mat.MaterialPrice; // editable field
                Persist();

                // if this row belongs to user's library, reflect update
                _ = UserLibrarySync.Instance.TryUpdateMaterialAsync(existing);
            }
        }


        private void Persist()
        {
            _ds.SaveMaterials(MaterialLibrary);
            MaterialLibraryService.Initialize(_ds);
            LibraryChanged?.Invoke();
            ApplyFilter();
        }

        //private async void UpdatePricesFromMongo()
        //{
        //    var result = MessageBox.Show(
        //        "Override prices with ADLM server values for your current zone?",
        //        "Confirm",
        //        MessageBoxButton.YesNo,
        //        MessageBoxImage.Question);

        //    if (result != MessageBoxResult.Yes)
        //        return;

        //    try
        //    {
        //        if (!NetChecks.IsOnline())
        //        {
        //            MessageBox.Show("You appear to be offline. Connect to the Internet to update prices.",
        //                "No Internet", MessageBoxButton.OK, MessageBoxImage.Warning);
        //            return;
        //        }

        //        string zone = ADLMRateGen.Properties.AppSettings.Zone ?? "";
        //        if (string.IsNullOrWhiteSpace(zone))
        //        {
        //            MessageBox.Show("No user zone set. Please sign in again to sync your zone profile.");
        //            return;
        //        }

        //        var auth = ADLMRateGen.Services.AuthProvider.Instance.Client;

        //        // NEW: refresh user rows from server in case another device added something
        //        await UserLibrarySync.Instance.LoadAsync();

        //        var masterDoc = await auth.GetJsonAsync($"/rategen/master?zone={Uri.EscapeDataString(zone)}");
        //        var root = masterDoc.RootElement;

        //        if (root.TryGetProperty("materials", out var mats))
        //            ADLMRateGen.Services.DataSourceCloudSync.SaveMaterialsFromDto(mats);

        //        ReloadFromDisk();
        //        MessageBox.Show($"Material prices updated for zone '{zone}'.");
        //    }
        //    catch (UnauthorizedAccessException)
        //    {
        //        MessageBox.Show(
        //            "Your session has expired. Please sign in again, then retry the update.",
        //            "Session expired", MessageBoxButton.OK, MessageBoxImage.Information);
        //    }
        //    catch (InvalidOperationException ex) when (ex.Message.StartsWith("401") || ex.Message.Contains("Not signed in"))
        //    {
        //        MessageBox.Show(
        //            "Not signed in. Please sign in again to update prices.",
        //            "Authentication required", MessageBoxButton.OK, MessageBoxImage.Warning);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Error updating materials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}


        private async void UpdatePricesFromMongo()
        {
            var result = MessageBox.Show(
                "Override prices with ADLM server values for your current zone?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Validate first (don’t show loader if we’ll immediately exit)
            if (!NetChecks.IsOnline())
            {
                MessageBox.Show("You appear to be offline. Connect to the Internet to update prices.",
                    "No Internet", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string zone = ADLMRateGen.Properties.AppSettings.Zone ?? "";
            if (string.IsNullOrWhiteSpace(zone))
            {
                MessageBox.Show("No user zone set. Please sign in again to sync your zone profile.");
                return;
            }

            BusyChanged?.Invoke(true, "Updating material prices from server…");

            try
            {
                var auth = ADLMRateGen.Services.AuthProvider.Instance.Client;

                // refresh user rows (another device could have added)
                await UserLibrarySync.Instance.LoadAsync();

                var masterDoc = await auth.GetJsonAsync($"/rategen/master?zone={Uri.EscapeDataString(zone)}");
                var root = masterDoc.RootElement;

                if (root.TryGetProperty("materials", out var mats))
                    ADLMRateGen.Services.DataSourceCloudSync.SaveMaterialsFromDto(mats);

                ReloadFromDisk();
                MessageBox.Show($"Material prices updated for zone '{zone}'.");
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Your session has expired. Please sign in again, then retry the update.",
                    "Session expired", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("401") || ex.Message.Contains("Not signed in"))
            {
                MessageBox.Show(
                    "Not signed in. Please sign in again to update prices.",
                    "Authentication required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating materials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BusyChanged?.Invoke(false, null);
            }
        }


        public void ReloadFromDisk()
        {
            MaterialLibrary.Clear();
            foreach (var m in _ds.LoadMaterials()) MaterialLibrary.Add(m);

            // keep the shared service in sync too
            MaterialLibraryService.Initialize(_ds);

            ApplyFilter();           // refresh CollectionView / grid
            LibraryChanged?.Invoke();// keep search index, etc., up to date
        }

        private void OpenNewMaterialDialog()
            => MessageBox.Show("TODO: open *Add Material* dialog");
    }
}
