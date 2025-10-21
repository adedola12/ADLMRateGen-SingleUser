using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.BlockWork;
using ADLMRateGen.ViewModel.ConcreteWork;
using ADLMRateGen.ViewModel.CustomRate;
using ADLMRateGen.ViewModel.Finishes;
using ADLMRateGen.ViewModel.Groundwork;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel.Painting;
using ADLMRateGen.ViewModel.RoofWork;
using ADLMRateGen.ViewModel.SteelWork;
using ADLMRateGen.ViewModel.WindowAndDoor;
using Microsoft.Win32;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        /* ───────── injected services ───────── */
        private readonly MongoDbService _mongoDbService;

        /* ───────── current user / auth ───────── */
        private bool _hasPriceNotifications;
        public bool HasPriceNotifications
        {
            get => _hasPriceNotifications;
            set { _hasPriceNotifications = value; RaisePropertyChanged(); }
        }

        private string _notificationMessage = "";
        public string NotificationMessage
        {
            get => _notificationMessage;
            set { _notificationMessage = value; RaisePropertyChanged(); }
        }

        private bool _isExportVisible;
        public bool IsExportVisible
        {
            get => _isExportVisible;
            set { _isExportVisible = value; RaisePropertyChanged(); }
        }

        private UserModel? _currentUser;
        public UserModel? CurrentUser
        {
            get => _currentUser;
            set
            {
                _currentUser = value;
                RaisePropertyChanged();             // ↺ notifies <Run …>
                RaisePropertyChanged(nameof(CurrentUsername));

                // show Export only on first login of month and +20 days thereafter
                if (_currentUser != null)
                    IsExportVisible = ExportVisibilityService.ShouldShowOnThisLogin(System.DateTime.Now);
                else
                    IsExportVisible = false;
            }
        }

        public ObservableCollection<string> Notifications { get; }
            = new ObservableCollection<string>();

        private bool _isNotificationsOpen;
        public bool IsNotificationsOpen
        {
            get => _isNotificationsOpen;
            set { _isNotificationsOpen = value; RaisePropertyChanged(); }
        }

        /* used directly by the banner if you prefer */
        public string CurrentUsername => _currentUser?.Username ?? string.Empty;

        /* ───────── login state ───────── */
        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; RaisePropertyChanged(); }
        }

        /* ───────── sidebar “active” flags ───────── */
        private bool _isLibraryShellActive;
        public bool IsLibraryShellActive
        {
            get => _isLibraryShellActive;
            set { _isLibraryShellActive = value; RaisePropertyChanged(); }
        }

        private bool _isMaterialInputActive;
        public bool IsMaterialInputActive
        {
            get => _isMaterialInputActive;
            set { _isMaterialInputActive = value; RaisePropertyChanged(); }
        }

        private bool _isMaterialLibraryActive;
        public bool IsMaterialLibraryActive
        {
            get => _isMaterialLibraryActive;
            set { _isMaterialLibraryActive = value; RaisePropertyChanged(); }
        }

        private bool _isLabourInputActive;
        public bool IsLabourInputActive
        {
            get => _isLabourInputActive;
            set { _isLabourInputActive = value; RaisePropertyChanged(); }
        }

        private bool _isLabourLibraryActive;
        public bool IsLabourLibraryActive
        {
            get => _isLabourLibraryActive;
            set { _isLabourLibraryActive = value; RaisePropertyChanged(); }
        }

        private bool _isGroundworkActive;
        public bool IsGroundworkActive
        {
            get => _isGroundworkActive;
            set { _isGroundworkActive = value; RaisePropertyChanged(); }
        }

        private bool _isConcreteViewActive;
        public bool IsConcreteViewActive
        {
            get => _isConcreteViewActive;
            set { _isConcreteViewActive = value; RaisePropertyChanged(); }
        }

        private bool _isBlockworkActive;
        public bool IsBlockworkActive
        {
            get => _isBlockworkActive;
            set { _isBlockworkActive = value; RaisePropertyChanged(); }
        }

        private bool _isFinishesActive;
        public bool IsFinishesActive
        {
            get => _isFinishesActive;
            set { _isFinishesActive = value; RaisePropertyChanged(); }
        }

        private bool _isRoofworkActive;
        public bool IsRoofworkActive
        {
            get => _isRoofworkActive;
            set { _isRoofworkActive = value; RaisePropertyChanged(); }
        }

        private bool _isWindowAndDoorActive;
        public bool IsWindowAndDoorActive
        {
            get => _isWindowAndDoorActive;
            set { _isWindowAndDoorActive = value; RaisePropertyChanged(); }
        }

        private bool _isPaintingActive;
        public bool IsPaintingActive
        {
            get => _isPaintingActive;
            set { _isPaintingActive = value; RaisePropertyChanged(); }
        }

        private bool _isSteelworkActive;
        public bool IsSteelworkActive
        {
            get => _isSteelworkActive;
            set { _isSteelworkActive = value; RaisePropertyChanged(); }
        }

        private bool _isCustomRateInputActive;
        public bool IsCustomRateInputActive
        {
            get => _isCustomRateInputActive;
            set { _isCustomRateInputActive = value; RaisePropertyChanged(); }
        }

        /* ───────── child view-models (public for binding) ───────── */
        public SignInViewModel SignInViewModel { get; }
        public MaterialPriceViewModel MaterialPriceViewModel { get; }
        public MaterialLibraryViewModel MaterialLibraryViewModel { get; }
        public LabourPriceViewModel LabourPriceViewModel { get; }
        public LabourLibraryViewModel LabourLibraryViewModel { get; }
        public LibraryShellViewModel LibraryShellViewModel { get; }
        public GroundWorkViewModel GroundWorkViewModel { get; }
        public ConcreteViewModel ConcreteViewModel { get; }
        public BlockworkViewModel BlockworkViewModel { get; }
        public FinishesViewModel FinishesViewModel { get; }
        public RoofWorkViewModel RoofWorkViewModel { get; }
        public WindowAndDoorViewModel WindowAndDoorViewModel { get; }
        public PaintWorkViewModel PaintWorkViewModel { get; }
        public SteelWorkViewModel SteelWorkViewModel { get; }
        public CustomRateEntryViewModel CustomRateEntryViewModel { get; }
        public CustomRateListViewModel CustomRateListViewModel { get; }
        public SearchBoxViewModel GlobalSearch { get; }

        private readonly SearchIndex _index = new();

        /* ───────── View switching ───────── */
        private ViewModelBase _selectedViewModel;
        public ViewModelBase SelectedViewModel
        {
            get => _selectedViewModel;
            set
            {
                _selectedViewModel = value;

                /* update all “active” flags */
                IsLibraryShellActive = value == LibraryShellViewModel;
                IsMaterialInputActive = value == MaterialPriceViewModel;
                IsMaterialLibraryActive = value == MaterialLibraryViewModel;
                IsLabourInputActive = value == LabourPriceViewModel;
                IsLabourLibraryActive = value == LabourLibraryViewModel;
                IsGroundworkActive = value == GroundWorkViewModel;
                IsConcreteViewActive = value == ConcreteViewModel;
                IsBlockworkActive = value == BlockworkViewModel;
                IsFinishesActive = value == FinishesViewModel;
                IsRoofworkActive = value == RoofWorkViewModel;
                IsWindowAndDoorActive = value == WindowAndDoorViewModel;
                IsPaintingActive = value == PaintWorkViewModel;
                IsSteelworkActive = value == SteelWorkViewModel;
                IsCustomRateInputActive = value == CustomRateListViewModel;

                RaisePropertyChanged();
            }
        }

        /* ───────── commands exposed to XAML ───────── */
        public ICommand SelectedMaterialInputViewCommand { get; }
        public ICommand SelectedMaterialLibraryViewCommand { get; }
        public ICommand SelectedLabourInputViewCommand { get; }
        public ICommand SelectedLabourLibraryViewCommand { get; }
        public ICommand SelectedLibraryShellViewCommand { get; }
        public ICommand SelectedGroundworkViewCommand { get; }
        public ICommand SelectedConcreteWorkViewCommand { get; }
        public ICommand SelectedBlockworkViewCommand { get; }
        public ICommand SelectedFinishesViewCommand { get; }
        public ICommand SelectedRoofworkViewCommand { get; }
        public ICommand SelectedWindowAndDoorViewCommand { get; }
        public ICommand SelectedPaintworkViewCommand { get; }
        public ICommand SelectedSteelworkViewCommand { get; }
        public ICommand SelectedCustomRateInputViewCommand { get; }
        public ICommand SelectedCustomRateViewCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenYoutubeCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand RefreshPricesCommand { get; }
        public ICommand ShowNotificationCommand { get; }
        public ICommand ToggleNotificationsCommand { get; }
        public ICommand DismissNotificationCommand { get; }
        public ICommand ExportAllRatesCommand { get; }
        public ICommand ExportBillCsvCommand { get; }


        /* ───────── ctor ───────── */
        public MainViewModel(
            MaterialPriceViewModel priceVM,
            MaterialLibraryViewModel libraryVM,
            LabourPriceViewModel labourVM,
            LabourLibraryViewModel labourLibVM,
            GroundWorkViewModel groundworkVM,
            ConcreteViewModel concreteVM,
            BlockworkViewModel blockworkVM,
            FinishesViewModel finishesVM,
            RoofWorkViewModel roofVM,
            WindowAndDoorViewModel winDoorVM,
            PaintWorkViewModel paintVM,
            SteelWorkViewModel steelVM,
            LibraryShellViewModel libraryShellVM,
            CustomRateListViewModel customListVM,
            CustomRateEntryViewModel customEntryVM,
            MongoDbService mongoDbService,
            SignInViewModel signInVM)
        {
            /* store deps */
            _mongoDbService = mongoDbService;

            Notifications = new ObservableCollection<string>();

            _mongoDbService.MaterialPricesChanged += () => AddNotification("New material prices available");
            _mongoDbService.LabourPricesChanged   += () => AddNotification("New labour prices available");

            ToggleNotificationsCommand = new RelayCommand(_ =>
            {
                IsNotificationsOpen = !IsNotificationsOpen;
            });

            DismissNotificationCommand = new RelayCommand(idxObj =>
            {
                if (idxObj is int i && i >= 0 && i < Notifications.Count)
                    Notifications.RemoveAt(i);
                IsNotificationsOpen = false;
            });

            ShowNotificationCommand = new RelayCommand(_ =>
            {
                MessageBox.Show(NotificationMessage, "Price Update");
                HasPriceNotifications = false;
            });

            /* ---------- create empty index & search VM ---------- */
            GlobalSearch = new SearchBoxViewModel(_index);

            /* assign child VMs */
            MaterialPriceViewModel = priceVM;
            MaterialLibraryViewModel = libraryVM;
            LabourPriceViewModel = labourVM;
            LabourLibraryViewModel = labourLibVM;
            LibraryShellViewModel = libraryShellVM;
            GroundWorkViewModel = groundworkVM;
            ConcreteViewModel = concreteVM;
            BlockworkViewModel = blockworkVM;
            FinishesViewModel = finishesVM;
            RoofWorkViewModel = roofVM;
            WindowAndDoorViewModel = winDoorVM;
            PaintWorkViewModel = paintVM;
            SteelWorkViewModel = steelVM;
            CustomRateListViewModel = customListVM;
            CustomRateEntryViewModel = customEntryVM;
            SignInViewModel = signInVM;

            MaterialLibraryViewModel.LibraryChanged += () => _index.Rebuild(this);
            LabourLibraryViewModel.LibraryChanged   += () => _index.Rebuild(this);
            CustomRateListViewModel.LibraryChanged  += () => _index.Rebuild(this);

            /* wire events (material / labour edit-flow etc.) */
            MaterialPriceViewModel.MaterialSaved += m => libraryVM.AddOrUpdateMaterial(m);
            libraryVM.EditMaterialRequested += OnEditMaterialRequested;
            labourVM.LabourSaved += l => labourLibVM.AddOrUpdateLabour(l);
            labourLibVM.EditLabourRequested += OnEditLabourRequested;

            customListVM.OnViewRequested += rate => { customEntryVM.LoadRate(rate); };

            // When SignIn succeeds, we get the authenticated user from SignInViewModel (which also did HW fingerprint checks)
            signInVM.LoginSucceeded += OnLoginSucceeded;

            signInVM.ZonePricesApplied += OnZonePricesApplied;


            /* default screen */
            SelectedViewModel = SignInViewModel;

            // Attempt local token auto-login on app start
            if (TryAutoLogin(out var tokenUser) && tokenUser != null)
            {
                IsLoggedIn = true;
                CurrentUser = new UserModel
                {
                    Id       = tokenUser.Id,
                    Email    = tokenUser.Email,
                    Username = !string.IsNullOrWhiteSpace(tokenUser.Username)
                        ? tokenUser.Username
                        : (tokenUser.Email?.Split('@')[0] ?? string.Empty)
                };

                // go straight to the library shell
                SelectedViewModel = LibraryShellViewModel;

                _ = UserLibrarySync.Instance.LoadAsync();

            }


            /* command implementations */
            SelectedMaterialInputViewCommand  = new RelayCommand(_ => SelectedViewModel = priceVM);
            SelectedMaterialLibraryViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);
            SelectedLibraryShellViewCommand    = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);

            SelectedLabourInputViewCommand    = new RelayCommand(_ => SelectedViewModel = labourVM);
            SelectedLabourLibraryViewCommand  = new RelayCommand(_ => SelectedViewModel = labourLibVM);
            SelectedGroundworkViewCommand     = new RelayCommand(_ => SelectedViewModel = groundworkVM);
            SelectedConcreteWorkViewCommand   = new RelayCommand(_ => SelectedViewModel = concreteVM);
            SelectedBlockworkViewCommand      = new RelayCommand(_ => SelectedViewModel = blockworkVM);
            SelectedFinishesViewCommand       = new RelayCommand(_ => SelectedViewModel = finishesVM);
            SelectedRoofworkViewCommand       = new RelayCommand(_ => SelectedViewModel = roofVM);
            SelectedWindowAndDoorViewCommand  = new RelayCommand(_ => SelectedViewModel = winDoorVM);
            SelectedPaintworkViewCommand      = new RelayCommand(_ => SelectedViewModel = paintVM);
            SelectedSteelworkViewCommand      = new RelayCommand(_ => SelectedViewModel = steelVM);
            SelectedCustomRateInputViewCommand= new RelayCommand(_ => SelectedViewModel = customListVM);
            SelectedCustomRateViewCommand     = new RelayCommand(_ => SelectedViewModel = customEntryVM);

            LogoutCommand       = new RelayCommand(_ => Logout());
            OpenYoutubeCommand  = new RelayCommand(_ => OpenYoutube());
            HelpCommand         = new RelayCommand(_ => SendHelpEmail());
            ExportAllRatesCommand = new RelayCommand(_ => ExportAllToExcel());
            ExportBillCsvCommand = new RelayCommand(_ => ExportBillToCsv());


            _index.Rebuild(this);

            /* whenever a library changes, rebuild */
            GroundWorkViewModel.PropertyChanged   += (_, __) => _index.Rebuild(this);
            ConcreteViewModel.PropertyChanged     += (_, __) => _index.Rebuild(this);
            BlockworkViewModel.PropertyChanged    += (_, __) => _index.Rebuild(this);
            FinishesViewModel.PropertyChanged     += (_, __) => _index.Rebuild(this);
            RoofWorkViewModel.PropertyChanged     += (_, __) => _index.Rebuild(this);
            WindowAndDoorViewModel.PropertyChanged+= (_, __) => _index.Rebuild(this);
            PaintWorkViewModel.PropertyChanged    += (_, __) => _index.Rebuild(this);
            SteelWorkViewModel.PropertyChanged    += (_, __) => _index.Rebuild(this);
        }

        private void OnZonePricesApplied(string zone)
        {
            // Ensure this runs on the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MaterialLibraryViewModel.ReloadFromDisk();
                LabourLibraryViewModel.ReloadFromDisk();

                // optionally show a toast/notification
                AddNotification($"Prices updated for {zone.Replace('_', ' ')}");
            });
        }


        private void AddNotification(string msg)
        {
            Notifications.Insert(0, msg);
            while (Notifications.Count > 5)
                Notifications.RemoveAt(Notifications.Count - 1);
            RaisePropertyChanged(nameof(Notifications));
            HasPriceNotifications = Notifications.Any();
        }

        /* ───────── auto-login helper ───────── */
        private bool TryAutoLogin(out UserModel? user)
        {
            user = null;

            var cfg = ConfigManager.LoadConfig();
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.AuthToken))
                return false;

            if (cfg.AuthExpiry < System.DateTime.Now)
                return false;

            var authTok = new AuthTok();
            var decoded = authTok.ValidateToken(cfg.AuthToken);
            if (decoded == null)
                return false;

            user = decoded;
            return true;
        }

        /* ───────── edit helpers ───────── */
        private void OnEditMaterialRequested(MaterialModel m)
        {
            //MaterialPriceViewModel.LoadForEdit(m);
            //SelectedViewModel = MaterialPriceViewModel;
        }

        private void OnEditLabourRequested(LabourModel l)
        {
            //LabourPriceViewModel.LoadForEdit(l);
            //SelectedViewModel = LabourPriceViewModel;
        }

        /* ───────── sign-in result ───────── */
        private void OnLoginSucceeded(object? s, SignInViewModel.LoginEventArgs e)
        {
            if (e.LoggedInUser == null) return;

            // Ensure username is never empty
            string ensuredUsername = !string.IsNullOrWhiteSpace(e.LoggedInUser.Username)
                ? e.LoggedInUser.Username
                : (e.LoggedInUser.Email?.Split('@')[0] ?? string.Empty);

            // Set CurrentUser now
            IsLoggedIn = true;
            CurrentUser = new UserModel
            {
                Id       = e.LoggedInUser.Id,
                Email    = e.LoggedInUser.Email,
                Username = ensuredUsername
            };

            // Persist an auth token (no need to immediately re-read/overwrite the user)
            var authTok = new AuthTok();
            ConfigManager.SaveConfig(new AppConfig
            {
                AuthToken  = authTok.GenerateAuthToken(CurrentUser),
                AuthExpiry = System.DateTime.Now.AddDays(15)
            });

            SelectedViewModel = LibraryShellViewModel;

            _ = UserLibrarySync.Instance.LoadAsync();

        }


        private void SendHelpEmail()
        {
            if (CurrentUser?.Email == null) return;

            var to = "admin@adlmstudio.net";
            var subject = System.Uri.EscapeDataString("Need help with ADLM Rate Gen");
            var body = System.Uri.EscapeDataString(
                $"Hello ADLM, my name is {CurrentUser.Email} and I need help with the ADLM Rate Gen.");
            var mailto = $"mailto:{to}?subject={subject}&body={body}";

            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
        }

        private void ExportAllToExcel()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Export ADLM Rates",
                    FileName = $"ADLM_Rates_{System.DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx"
                };
                if (sfd.ShowDialog() != true) return;

                var sheets = new List<ExcelExporter.ExportSheet>();

                void AddSheet(string name, object vm)
                {
                    var rows = FindRowsEnumerable(vm);
                    if (rows != null && rows.Cast<object>().Any())
                        sheets.Add(new ExcelExporter.ExportSheet(name, rows));
                }

                AddSheet("Ground", GroundWorkViewModel);
                AddSheet("Concrete", ConcreteViewModel);
                AddSheet("Block Works", BlockworkViewModel);
                AddSheet("Finishes", FinishesViewModel);
                AddSheet("Roofs", RoofWorkViewModel);
                AddSheet("Painting", PaintWorkViewModel);
                AddSheet("Steel", SteelWorkViewModel);
                AddSheet("Window & Door", WindowAndDoorViewModel);
                sheets.Add(new ExcelExporter.ExportSheet("Saved Rates", CustomRateListViewModel.CustomRates));

                if (!sheets.Any())
                {
                    MessageBox.Show("No data available to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ExcelExporter.ExportWorkbook(sheets, sfd.FileName);
                MessageBox.Show("Export completed.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportBillToCsv()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Export to Bill (CSV)",
                    FileName = $"ADLM_Bill_{System.DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Filter = "CSV (*.csv)|*.csv"
                };
                if (sfd.ShowDialog() != true) return;

                // Collect all sections
                var sections = new List<(string Name, IEnumerable Rows)>();

                void AddSection(string name, object vm)
                {
                    var rows = FindRowsEnumerable(vm);
                    if (rows != null) sections.Add((name, rows));
                }

                // use the same VMs you export to Excel
                AddSection("Ground", GroundWorkViewModel);
                AddSection("Concrete", ConcreteViewModel);
                AddSection("Block Works", BlockworkViewModel);
                AddSection("Finishes", FinishesViewModel);
                AddSection("Roofs", RoofWorkViewModel);
                AddSection("Painting", PaintWorkViewModel);
                AddSection("Steel", SteelWorkViewModel);
                AddSection("Window & Door", WindowAndDoorViewModel);

                // Custom (saved) rates: we know the collection directly
                if (CustomRateListViewModel?.CustomRates is IEnumerable cr && cr.GetEnumerator().MoveNext())
                    sections.Add(("Saved Rates", CustomRateListViewModel.CustomRates));

                if (sections.Count == 0)
                {
                    MessageBox.Show("No data available to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Build CSV
                var sb = new StringBuilder();
                sb.AppendLine("Section,Description,Total");

                foreach (var (name, rows) in sections)
                {
                    foreach (var row in rows)
                    {
                        if (row == null) continue;

                        var desc = GetStringProp(row, "Description", "Name", "Title") ?? string.Empty;
                        var total = GetDecimalProp(row, "TotalCost", "TotalPrice", "Total"); // extend names if needed

                        sb.AppendLine($"{Csv(name)},{Csv(desc)},{total.ToString("0.##", CultureInfo.InvariantCulture)}");
                    }
                }

                // Write with UTF-8 BOM so Excel opens it nicely
                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                MessageBox.Show("CSV exported.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // ---- helpers ----
            static string Csv(string s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";

            static string? GetStringProp(object o, params string[] names)
            {
                var t = o.GetType();
                foreach (var n in names)
                {
                    var p = t.GetProperty(n);
                    if (p == null) continue;
                    var v = p.GetValue(o);
                    if (v != null) return v.ToString();
                }
                return null;
            }

            static decimal GetDecimalProp(object o, params string[] names)
            {
                var t = o.GetType();
                foreach (var n in names)
                {
                    var p = t.GetProperty(n);
                    if (p == null) continue;
                    var v = p.GetValue(o);
                    if (v == null) continue;

                    try
                    {
                        // supports decimal/double/float/int, etc.
                        return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
                    }
                    catch { /* try next name */ }
                }
                return 0m;
            }
        }

        /// <summary>
        /// Finds the first IEnumerable property on a VM whose element type
        /// has ItemNo + (Description|Name|Title) + (TotalCost|TotalPrice).
        /// Works with ObservableCollection and ICollectionView.
        /// </summary>
        private static IEnumerable? FindRowsEnumerable(object vm)
        {
            if (vm == null) return null;

            foreach (var p in vm.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var pt = p.PropertyType;
                if (pt == typeof(string)) continue;
                if (!typeof(IEnumerable).IsAssignableFrom(pt)) continue;

                var val = p.GetValue(vm) as IEnumerable;
                if (val == null) continue;

                object? first = null;
                foreach (var r in val) { first = r; if (first != null) break; }
                if (first == null) continue;

                var t = first.GetType();
                bool hasSn = t.GetProperty("ItemNo") != null || t.GetProperty("SNo") != null;
                bool hasDesc = t.GetProperty("Description") != null || t.GetProperty("Name") != null || t.GetProperty("Title") != null;
                bool hasTot = t.GetProperty("TotalCost") != null || t.GetProperty("TotalPrice") != null;

                if (hasSn && hasDesc && hasTot)
                    return val;
            }
            return null;
        }

        private void Logout()
        {
            try
            {
                ConfigManager.ClearConfig();
                _currentUser = null;

                IsLoggedIn = false;
                SelectedViewModel = SignInViewModel;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Log-out failed\n{ex.Message}");
            }
        }

        private static void OpenYoutube()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://youtube.com/playlist?list=PLk1KkUNE9ZrO5IPh7p3-5zxfDFs1Dl9b-&si=xi4dt-Fmy17_2KsM",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to open browser.\n{ex.Message}");
            }
        }
    }
}
