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
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private const string API_BASE_URL = "https://adlmweb.onrender.com";

        // ✅ New clean public base
        private const string RATEGEN_V2_BASE = "/rategen-v2";

        // ✅ Public compute-items endpoint (matches your server mounting)
        private const string COMPUTE_ITEMS_PATH = RATEGEN_V2_BASE + "/compute-items";

        // ✅ Public library meta (since you moved library router to /rategen-v2)
        private const string LIBRARY_META_PATH = RATEGEN_V2_BASE + "/library/meta";

        private const string RATE_LIBRARY_SYNC_PATH = RATEGEN_V2_BASE + "/library/rates/sync";



        /* ───────── injected services ───────── */
        private readonly MongoDbService _mongoDbService;

        /* ───────── rate sync ───────── */
        private readonly RateCatalogSyncService _rateSync;

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
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(CurrentUsername));

                if (_currentUser != null)
                    IsExportVisible = ExportVisibilityService.ShouldShowOnThisLogin(DateTime.Now);
                else
                    IsExportVisible = false;
            }
        }

        public ICommand RefreshCloudDataCommand { get; }

        private string _cloudSyncStatus = "Cloud sync: not started";
        public string CloudSyncStatus
        {
            get => _cloudSyncStatus;
            set { _cloudSyncStatus = value; RaisePropertyChanged(); }
        }

        private DateTime? _lastCloudSyncAt;
        public DateTime? LastCloudSyncAt
        {
            get => _lastCloudSyncAt;
            set { _lastCloudSyncAt = value; RaisePropertyChanged(); }
        }


        public ObservableCollection<string> Notifications { get; } = new ObservableCollection<string>();

        private bool _isNotificationsOpen;
        public bool IsNotificationsOpen
        {
            get => _isNotificationsOpen;
            set { _isNotificationsOpen = value; RaisePropertyChanged(); }
        }

        public string CurrentUsername => _currentUser?.Username ?? string.Empty;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; RaisePropertyChanged(); }
        }

        private string _busyMessage = "Loading…";
        public string BusyMessage
        {
            get => _busyMessage;
            set { _busyMessage = value; RaisePropertyChanged(); }
        }

        private void SetBusy(bool isBusy, string? message = null)
        {
            var app = Application.Current;
            if (app?.Dispatcher == null)
            {
                IsBusy = isBusy;
                if (!string.IsNullOrWhiteSpace(message)) BusyMessage = message;
                return;
            }

            if (app.Dispatcher.CheckAccess())
            {
                IsBusy = isBusy;
                if (!string.IsNullOrWhiteSpace(message)) BusyMessage = message;
            }
            else
            {
                app.Dispatcher.Invoke(() =>
                {
                    IsBusy = isBusy;
                    if (!string.IsNullOrWhiteSpace(message)) BusyMessage = message;
                });
            }
        }

        /* ───────── login state ───────── */
        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; RaisePropertyChanged(); }
        }

        /* ───────── sidebar “active” flags ───────── */
        private bool _isLibraryShellActive;
        public bool IsLibraryShellActive { get => _isLibraryShellActive; set { _isLibraryShellActive = value; RaisePropertyChanged(); } }

        private bool _isMaterialInputActive;
        public bool IsMaterialInputActive { get => _isMaterialInputActive; set { _isMaterialInputActive = value; RaisePropertyChanged(); } }

        private bool _isMaterialLibraryActive;
        public bool IsMaterialLibraryActive { get => _isMaterialLibraryActive; set { _isMaterialLibraryActive = value; RaisePropertyChanged(); } }

        private bool _isLabourInputActive;
        public bool IsLabourInputActive { get => _isLabourInputActive; set { _isLabourInputActive = value; RaisePropertyChanged(); } }

        private bool _isLabourLibraryActive;
        public bool IsLabourLibraryActive { get => _isLabourLibraryActive; set { _isLabourLibraryActive = value; RaisePropertyChanged(); } }

        private bool _isGroundworkActive;
        public bool IsGroundworkActive { get => _isGroundworkActive; set { _isGroundworkActive = value; RaisePropertyChanged(); } }

        private bool _isConcreteViewActive;
        public bool IsConcreteViewActive { get => _isConcreteViewActive; set { _isConcreteViewActive = value; RaisePropertyChanged(); } }

        private bool _isBlockworkActive;
        public bool IsBlockworkActive { get => _isBlockworkActive; set { _isBlockworkActive = value; RaisePropertyChanged(); } }

        private bool _isFinishesActive;
        public bool IsFinishesActive { get => _isFinishesActive; set { _isFinishesActive = value; RaisePropertyChanged(); } }

        private bool _isRoofworkActive;
        public bool IsRoofworkActive { get => _isRoofworkActive; set { _isRoofworkActive = value; RaisePropertyChanged(); } }

        private bool _isWindowAndDoorActive;
        public bool IsWindowAndDoorActive { get => _isWindowAndDoorActive; set { _isWindowAndDoorActive = value; RaisePropertyChanged(); } }

        private bool _isPaintingActive;
        public bool IsPaintingActive { get => _isPaintingActive; set { _isPaintingActive = value; RaisePropertyChanged(); } }

        private bool _isSteelworkActive;
        public bool IsSteelworkActive { get => _isSteelworkActive; set { _isSteelworkActive = value; RaisePropertyChanged(); } }

        private bool _isCustomRateInputActive;
        public bool IsCustomRateInputActive { get => _isCustomRateInputActive; set { _isCustomRateInputActive = value; RaisePropertyChanged(); } }

        /* ───────── child view-models ───────── */
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

        /* ───────── commands ───────── */
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
        public ICommand ShowNotificationCommand { get; }
        public ICommand ToggleNotificationsCommand { get; }
        public ICommand DismissNotificationCommand { get; }
        public ICommand ExportAllRatesCommand { get; }
        public ICommand ExportBillCsvCommand { get; }
        public ICommand RunSanityCheckCommand { get; }

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
            _mongoDbService = mongoDbService;

            // ✅ Create rate sync service once
            _rateSync = new RateCatalogSyncService(new HttpClient(), API_BASE_URL);

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

            GlobalSearch = new SearchBoxViewModel(_index);

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

            // ✅ notifications from local MongoDbService events
            _mongoDbService.MaterialPricesChanged += () => AddNotification("New material prices available");
            _mongoDbService.LabourPricesChanged += () => AddNotification("New labour prices available");

            // ✅ keep index updated when libraries change
            MaterialLibraryViewModel.LibraryChanged += () => _index.Rebuild(this);
            LabourLibraryViewModel.LibraryChanged += () => _index.Rebuild(this);
            CustomRateListViewModel.LibraryChanged += () => _index.Rebuild(this);

            libraryVM.BusyChanged += (busy, msg) => SetBusy(busy, msg);
            labourLibVM.BusyChanged += (busy, msg) => SetBusy(busy, msg);

            MaterialPriceViewModel.MaterialSaved += m => libraryVM.AddOrUpdateMaterial(m);
            libraryVM.EditMaterialRequested += OnEditMaterialRequested;
            labourVM.LabourSaved += l => labourLibVM.AddOrUpdateLabour(l);
            labourLibVM.EditLabourRequested += OnEditLabourRequested;

            customListVM.OnViewRequested += rate => { customEntryVM.LoadRate(rate); };

            signInVM.LoginSucceeded += OnLoginSucceeded;
            signInVM.ZonePricesApplied += OnZonePricesApplied;

            SelectedViewModel = SignInViewModel;

            // ✅ Configure compute store to the ACTUAL endpoint
            ComputeCatalogStore.ConfigureApi(API_BASE_URL, COMPUTE_ITEMS_PATH);
            ComputeCatalogStore.ReloadFromDisk(); // load cached first

            // ✅ Rate library (admin-created rates)
            RateLibraryStore.ConfigureApi(API_BASE_URL, RATE_LIBRARY_SYNC_PATH);
            RateLibraryStore.ReloadFromDisk();


            // ✅ When rate catalog updates, reload libraries and rebuild index
            _rateSync.CatalogUpdated += msg =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddNotification(msg);

                    MaterialLibraryViewModel.ReloadFromDisk();
                    LabourLibraryViewModel.ReloadFromDisk();
                    ComputeCatalogStore.ReloadFromDisk();

                    _index.Rebuild(this);
                });
            };

            // ✅ auto-login
            if (TryAutoLogin(out var tokenUser) && tokenUser != null)
            {
                IsLoggedIn = true;
                CurrentUser = new UserModel
                {
                    Id = tokenUser.Id,
                    Email = tokenUser.Email,
                    Username = !string.IsNullOrWhiteSpace(tokenUser.Username)
                        ? tokenUser.Username
                        : (tokenUser.Email?.Split('@')[0] ?? string.Empty)
                };

                SelectedViewModel = LibraryShellViewModel;
                _ = UserLibrarySync.Instance.LoadAsync();

                // ✅ optional: also check for updates on app start (auto-login path)
                _ = TryCheckUpdatesAfterLoginAsync();
            }

            // navigation
            SelectedMaterialInputViewCommand = new RelayCommand(_ => SelectedViewModel = priceVM);
            SelectedMaterialLibraryViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);
            SelectedLibraryShellViewCommand = new RelayCommand(_ => SelectedViewModel = LibraryShellViewModel);
            SelectedLabourInputViewCommand = new RelayCommand(_ => SelectedViewModel = labourVM);
            SelectedLabourLibraryViewCommand = new RelayCommand(_ => SelectedViewModel = labourLibVM);
            SelectedGroundworkViewCommand = new RelayCommand(_ => SelectedViewModel = groundworkVM);
            SelectedConcreteWorkViewCommand = new RelayCommand(_ => SelectedViewModel = concreteVM);
            SelectedBlockworkViewCommand = new RelayCommand(_ => SelectedViewModel = blockworkVM);
            SelectedFinishesViewCommand = new RelayCommand(_ => SelectedViewModel = finishesVM);
            SelectedRoofworkViewCommand = new RelayCommand(_ => SelectedViewModel = roofVM);
            SelectedWindowAndDoorViewCommand = new RelayCommand(_ => SelectedViewModel = winDoorVM);
            SelectedPaintworkViewCommand = new RelayCommand(_ => SelectedViewModel = paintVM);
            SelectedSteelworkViewCommand = new RelayCommand(_ => SelectedViewModel = steelVM);
            SelectedCustomRateInputViewCommand = new RelayCommand(_ => SelectedViewModel = customListVM);
            SelectedCustomRateViewCommand = new RelayCommand(_ => SelectedViewModel = customEntryVM);
            RefreshCloudDataCommand = new RelayCommand(async _ => await RefreshCloudDataAsync(manual: true));


            LogoutCommand = new RelayCommand(_ => Logout());
            OpenYoutubeCommand = new RelayCommand(_ => OpenYoutube());
            HelpCommand = new RelayCommand(_ => SendHelpEmail());
            ExportAllRatesCommand = new RelayCommand(_ => ExportAllToExcel());
            ExportBillCsvCommand = new RelayCommand(_ => ExportBillToCsv());

            RunSanityCheckCommand = new RelayCommand(async _ => await RunSanityCheckAsync());

            // initial index
            _index.Rebuild(this);

            // (optional) index rebuild when any section changes
            GroundWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            ConcreteViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            BlockworkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            FinishesViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            RoofWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            WindowAndDoorViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            PaintWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
            SteelWorkViewModel.PropertyChanged += (_, __) => _index.Rebuild(this);
        }

        private static bool Is404(Exception ex)
        {
            if (ex is HttpRequestException hre)
            {
#if NET8_0_OR_GREATER
                if (hre.StatusCode.HasValue && hre.StatusCode.Value == System.Net.HttpStatusCode.NotFound)
                    return true;
#endif
                // fallback if StatusCode isn't available in your build
                if ((hre.Message ?? "").Contains("404")) return true;
            }
            return (ex.Message ?? "").Contains("404");
        }

        private async Task RefreshCloudDataAsync(bool manual)
        {
            var results = new List<string>();

            try
            {
                var cfg = ConfigManager.LoadConfig() ?? new AppConfig();
                var token = cfg.AuthToken;

                if (string.IsNullOrWhiteSpace(token))
                {
                    CloudSyncStatus = "Cloud sync: skipped (no auth token). Please sign in again.";
                    if (manual)
                        MessageBox.Show("No auth token saved. Sign in again so RateGen can sync your cloud rates.",
                            "Sync from Cloud", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetBusy(true, manual ? "Syncing from cloud…" : "Syncing after login…");
                CloudSyncStatus = "Cloud sync: running…";

                // ✅ 1) Admin-created Rate Library (DB rates)
                bool ratesOk = false;
                try
                {
                    RateLibraryStore.ReloadFromDisk();
                    ratesOk = await RateLibraryStore.RefreshFromApiAsync(); // fetch all sections
                    RateLibraryStore.ReloadFromDisk();

                    results.Add($"Rates: {(ratesOk ? "OK" : "FAIL")} ({RateLibraryStore.LastApiItemCount})");
                    if (!ratesOk) results.Add($"Rates msg: {RateLibraryStore.LastApiMessage}");
                }
                catch (Exception ex)
                {
                    results.Add($"Rates: FAIL (exception) - {ex.Message}");
                }

                // ✅ 2) Compute Catalog (may be your 404 source)
                bool computeOk = false;
                try
                {
                    ComputeCatalogStore.ReloadFromDisk();
                    computeOk = await ComputeCatalogStore.RefreshFromApiAsync();
                    ComputeCatalogStore.ReloadFromDisk();

                    results.Add($"Compute: {(computeOk ? "OK" : "FAIL")} ({ComputeCatalogStore.LastApiItemCount})");
                    if (!computeOk) results.Add($"Compute msg: {ComputeCatalogStore.LastApiMessage}");
                }
                catch (Exception ex)
                {
                    // if this is the 404, we don’t want to kill the entire sync
                    if (Is404(ex))
                        results.Add("Compute: SKIPPED (404 Not Found – endpoint route mismatch)");
                    else
                        results.Add($"Compute: FAIL (exception) - {ex.Message}");
                }

                // ✅ 3) Optional: materials/labour update check (this is often another 404 source)
                try
                {
                    var zone = cfg.Zone ?? "Lagos";

                    await _rateSync.CheckAndPromptUpdateAsync(
                        token,
                        zone,
                        async (prompt) =>
                        {
                            var result = MessageBox.Show(prompt, "Rate Update",
                                MessageBoxButton.YesNo, MessageBoxImage.Information);
                            return await Task.FromResult(result == MessageBoxResult.Yes);
                        });

                    results.Add("Materials/Labour check: OK");
                }
                catch (Exception ex)
                {
                    if (Is404(ex))
                        results.Add("Materials/Labour check: SKIPPED (404 Not Found – still pointing to old route)");
                    else
                        results.Add($"Materials/Labour check: FAIL - {ex.Message}");
                }

                // Refresh local UI
                MaterialLibraryViewModel.ReloadFromDisk();
                LabourLibraryViewModel.ReloadFromDisk();
                _index.Rebuild(this);

                LastCloudSyncAt = DateTime.Now;

                CloudSyncStatus = "Cloud sync: done | " + string.Join(" | ", results.Where(x => x.StartsWith("Rates:") || x.StartsWith("Compute:")));
                AddNotification(CloudSyncStatus);

                if (manual)
                {
                    MessageBox.Show(
                        string.Join("\n", results),
                        "Sync from Cloud",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CloudSyncStatus = $"Cloud sync: failed ({ex.Message})";
                AddNotification(CloudSyncStatus);

                if (manual)
                    MessageBox.Show($"Sync failed:\n{ex.Message}", "Sync from Cloud",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OnZonePricesApplied(string zone)
        {
            SetBusy(true, $"Updating prices for {zone.Replace('_', ' ')}…");

            Application.Current.Dispatcher.Invoke(() =>
            {
                MaterialLibraryViewModel.ReloadFromDisk();
                LabourLibraryViewModel.ReloadFromDisk();
                ComputeCatalogStore.ReloadFromDisk();
                AddNotification($"Prices updated for {zone.Replace('_', ' ')}");
            });

            _index.Rebuild(this);
            SetBusy(false);
        }

        private void AddNotification(string msg)
        {
            Notifications.Insert(0, msg);
            while (Notifications.Count > 5)
                Notifications.RemoveAt(Notifications.Count - 1);

            RaisePropertyChanged(nameof(Notifications));
            HasPriceNotifications = Notifications.Any();
        }

        private bool TryAutoLogin(out UserModel? user)
        {
            user = null;

            var cfg = ConfigManager.LoadConfig();
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.AuthToken))
                return false;

            if (cfg.AuthExpiry < DateTime.Now)
                return false;

            var authTok = new AuthTok();
            var decoded = authTok.ValidateToken(cfg.AuthToken);
            if (decoded == null)
                return false;

            user = decoded;
            return true;
        }

        private void OnEditMaterialRequested(MaterialModel m) { }
        private void OnEditLabourRequested(LabourModel l) { }

        //private async void OnLoginSucceeded(object? s, SignInViewModel.LoginEventArgs e)
        //{
        //    if (e.LoggedInUser == null) return;

        //    string ensuredUsername = !string.IsNullOrWhiteSpace(e.LoggedInUser.Username)
        //        ? e.LoggedInUser.Username
        //        : (e.LoggedInUser.Email?.Split('@')[0] ?? string.Empty);

        //    IsLoggedIn = true;
        //    CurrentUser = new UserModel
        //    {
        //        Id = e.LoggedInUser.Id,
        //        Email = e.LoggedInUser.Email,
        //        Username = ensuredUsername
        //    };

        //    // keep your existing token storage logic
        //    var authTok = new AuthTok();
        //    ConfigManager.SaveConfig(new AppConfig
        //    {
        //        AuthToken = authTok.GenerateAuthToken(CurrentUser),
        //        AuthExpiry = DateTime.Now.AddDays(15)
        //    });

        //    SelectedViewModel = LibraryShellViewModel;

        //    // Load cached libraries immediately
        //    _ = UserLibrarySync.Instance.LoadAsync();

        //    // Now do API update checks + compute refresh
        //    await TryCheckUpdatesAfterLoginAsync();
        //}

        private async void OnLoginSucceeded(object? s, SignInViewModel.LoginEventArgs e)
        {
            if (e.LoggedInUser == null) return;

            string ensuredUsername = !string.IsNullOrWhiteSpace(e.LoggedInUser.Username)
                ? e.LoggedInUser.Username
                : (e.LoggedInUser.Email?.Split('@')[0] ?? string.Empty);

            IsLoggedIn = true;
            CurrentUser = new UserModel
            {
                Id = e.LoggedInUser.Id,
                Email = e.LoggedInUser.Email,
                Username = ensuredUsername
            };

            // ✅ SAVE SERVER TOKEN (this fixes your 401)
            var expiry = TryGetJwtExpiryLocal(e.AccessToken) ?? DateTime.Now.AddMinutes(25);

            ConfigManager.SaveConfig(new AppConfig
            {
                AuthToken = e.AccessToken,
                AuthExpiry = expiry
                // Zone stays as-is if you already store it
            });

            SelectedViewModel = LibraryShellViewModel;

            // Load cached first
            _ = UserLibrarySync.Instance.LoadAsync();

            // Now re-check APIs with valid token
            await TryCheckUpdatesAfterLoginAsync();

            await RefreshCloudDataAsync(manual: false);

        }

        // Reads JWT exp without validating signature (good enough for local expiry)
        private static DateTime? TryGetJwtExpiryLocal(string jwt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jwt)) return null;
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(jwt);
                var exp = token.Payload.Exp;
                if (exp == null) return null;
                var utc = DateTimeOffset.FromUnixTimeSeconds((long)exp).UtcDateTime;
                return utc.ToLocalTime();
            }
            catch { return null; }
        }


        // ✅ This is the real “does it fetch rates + apply to library” flow
        private async Task TryCheckUpdatesAfterLoginAsync()
        {
            try
            {
                var cfg = ConfigManager.LoadConfig() ?? new AppConfig();
                var zone = cfg.Zone ?? "Lagos";
                var token = cfg.AuthToken;

                if (string.IsNullOrWhiteSpace(token))
                {
                    AddNotification("⚠ Update check skipped: missing token.");
                    return;
                }

                SetBusy(true, "Checking for latest library + compute updates…");

                // 1) Probe library meta endpoint (token/entitlement check)
                var metaOk = await ProbeLibraryMetaAsync(token);
                if (!metaOk)
                    AddNotification($"⚠ Token/entitlement probe failed for GET {LIBRARY_META_PATH}");

                // 2) Sync materials/labour library (writes to disk) + triggers CatalogUpdated
                await _rateSync.CheckAndPromptUpdateAsync(
                    token,
                    zone,
                    async (prompt) =>
                    {
                        var result = MessageBox.Show(
                            prompt,
                            "Rate Update",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        return await Task.FromResult(result == MessageBoxResult.Yes);
                    });

                // 3) Refresh compute items
                var ok = await ComputeCatalogStore.RefreshFromApiAsync();
                AddNotification(ok
                    ? $"✅ Compute items updated ({ComputeCatalogStore.LastApiItemCount})"
                    : $"⚠ Compute update failed: {ComputeCatalogStore.LastApiMessage}");

                // 4) Ensure UI uses latest disk snapshot
                RateLibraryStore.ReloadFromDisk();
                MaterialLibraryViewModel.ReloadFromDisk();
                LabourLibraryViewModel.ReloadFromDisk();
                ComputeCatalogStore.ReloadFromDisk();

                // 3b) Refresh admin-created Rate Library (rategenrates)
                var ratesOk = await RateLibraryStore.RefreshFromApiAsync();
                AddNotification(ratesOk
                    ? $"✅ Rate library updated ({RateLibraryStore.LastApiItemCount})"
                    : $"⚠ Rate library update failed: {RateLibraryStore.LastApiMessage}");


                _index.Rebuild(this);
            }
            catch (Exception ex)
            {
                AddNotification($"Update check failed: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ✅ Probe using an explicit Bearer token (used by sanity check + update flow)
        private static async Task<bool> ProbeLibraryMetaAsync(string bearerToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(bearerToken))
                    return false;

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", bearerToken);
                http.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var resp = await http.GetAsync($"{API_BASE_URL}{LIBRARY_META_PATH}");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ✅ Optional: probe using your AuthProvider client (if you already use it elsewhere)
        private static async Task<bool> ProbeLibraryMetaAsync()
        {
            try
            {
                await AuthProvider.Instance.Client.GetJsonAsync(LIBRARY_META_PATH);
                return true;
            }
            catch
            {
                return false;
            }
        }



        private async Task RunSanityCheckAsync()
        {
            try
            {
                SetBusy(true, "Running sanity check...");

                var issues = new List<string>();

                // 1) ViewModels present
                if (MaterialPriceViewModel == null) issues.Add("MaterialPriceViewModel is null");
                if (MaterialLibraryViewModel == null) issues.Add("MaterialLibraryViewModel is null");
                if (LabourPriceViewModel == null) issues.Add("LabourPriceViewModel is null");
                if (LabourLibraryViewModel == null) issues.Add("LabourLibraryViewModel is null");
                if (GroundWorkViewModel == null) issues.Add("GroundWorkViewModel is null");
                if (ConcreteViewModel == null) issues.Add("ConcreteViewModel is null");
                if (BlockworkViewModel == null) issues.Add("BlockworkViewModel is null");
                if (FinishesViewModel == null) issues.Add("FinishesViewModel is null");
                if (RoofWorkViewModel == null) issues.Add("RoofWorkViewModel is null");
                if (WindowAndDoorViewModel == null) issues.Add("WindowAndDoorViewModel is null");
                if (PaintWorkViewModel == null) issues.Add("PaintWorkViewModel is null");
                if (SteelWorkViewModel == null) issues.Add("SteelWorkViewModel is null");
                if (CustomRateListViewModel == null) issues.Add("CustomRateListViewModel is null");
                if (CustomRateEntryViewModel == null) issues.Add("CustomRateEntryViewModel is null");
                if (LibraryShellViewModel == null) issues.Add("LibraryShellViewModel is null");

                // 2) Disk files existence
                var computePath = ComputeCatalogStore.FilePath;
                if (!File.Exists(computePath))
                    issues.Add($"Compute items file missing: {computePath}");

                // 3) Load compute items
                ComputeCatalogStore.ReloadFromDisk();
                var items = ComputeCatalogStore.Items; // IReadOnlyList<ComputeItemDefinition>
                if (items == null || items.Count == 0)
                    issues.Add("Compute items loaded = 0 (compute-items.json empty or invalid).");

                // 4) API reachability + auth checks
                var cfg = ConfigManager.LoadConfig();
                var token = cfg?.AuthToken;

                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    var ping = await http.GetAsync($"{API_BASE_URL}/__debug/db");
                    if (!ping.IsSuccessStatusCode)
                        issues.Add($"API reachable but /__debug/db returned {(int)ping.StatusCode}");
                }
                catch (Exception ex)
                {
                    issues.Add($"API check failed: {ex.Message}");
                }

                // 5) Token/entitlement checks
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var metaOk = await ProbeLibraryMetaAsync(token);
                    if (!metaOk)
                        issues.Add($"Token/entitlement probe failed for GET {LIBRARY_META_PATH}");

                    // Compute endpoint status check
                    try
                    {
                        using var http2 = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                        http2.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", token);
                        http2.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));

                        var cResp = await http2.GetAsync($"{API_BASE_URL}{COMPUTE_ITEMS_PATH}");
                        if (!cResp.IsSuccessStatusCode)
                            issues.Add($"Compute endpoint failed: GET {COMPUTE_ITEMS_PATH} => {(int)cResp.StatusCode}");
                    }
                    catch (Exception ex)
                    {
                        issues.Add($"Compute endpoint check threw: {ex.Message}");
                    }
                }
                else
                {
                    issues.Add("Skipped auth checks: no saved auth token (not logged in).");
                }

                // Rate Library endpoint status check
                try
                {
                    using var http3 = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    http3.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                    http3.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    var rResp = await http3.GetAsync($"{API_BASE_URL}{RATE_LIBRARY_SYNC_PATH}?limit=1&sectionKey=ground");
                    if (!rResp.IsSuccessStatusCode)
                        issues.Add($"Rate library endpoint failed: GET {RATE_LIBRARY_SYNC_PATH} => {(int)rResp.StatusCode}");
                }
                catch (Exception ex)
                {
                    issues.Add($"Rate library endpoint check threw: {ex.Message}");
                }


                if (issues.Count == 0)
                {
                    MessageBox.Show(
                        "✅ Sanity Check PASSED\n\nLibraries + compute file detected.\nAPI reachable.\nMeta endpoint probe passed.",
                        "ADLM RateGen · Sanity Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                MessageBox.Show(
                    "⚠️ Sanity Check FOUND ISSUES:\n\n- " + string.Join("\n- ", issues),
                    "ADLM RateGen · Sanity Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            finally
            {
                SetBusy(false);
            }
        }


        private void SendHelpEmail()
        {
            if (CurrentUser?.Email == null) return;

            var to = "admin@adlmstudio.net";
            var subject = Uri.EscapeDataString("Need help with ADLM Rate Gen");
            var body = Uri.EscapeDataString($"Hello ADLM, my name is {CurrentUser.Email} and I need help with the ADLM Rate Gen.");
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
                    FileName = $"ADLM_Rates_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
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
            catch (Exception ex)
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
                    FileName = $"ADLM_Bill_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Filter = "CSV (*.csv)|*.csv"
                };
                if (sfd.ShowDialog() != true) return;

                var sections = new List<(string Name, IEnumerable Rows)>();

                void AddSection(string name, object vm)
                {
                    var rows = FindRowsEnumerable(vm);
                    if (rows != null) sections.Add((name, rows));
                }

                AddSection("Ground", GroundWorkViewModel);
                AddSection("Concrete", ConcreteViewModel);
                AddSection("Block Works", BlockworkViewModel);
                AddSection("Finishes", FinishesViewModel);
                AddSection("Roofs", RoofWorkViewModel);
                AddSection("Painting", PaintWorkViewModel);
                AddSection("Steel", SteelWorkViewModel);
                AddSection("Window & Door", WindowAndDoorViewModel);

                if (CustomRateListViewModel?.CustomRates is IEnumerable cr && cr.GetEnumerator().MoveNext())
                    sections.Add(("Saved Rates", CustomRateListViewModel.CustomRates));

                if (sections.Count == 0)
                {
                    MessageBox.Show("No data available to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Section,Description,Total");

                foreach (var (name, rows) in sections)
                {
                    foreach (var row in rows)
                    {
                        if (row == null) continue;

                        var desc = GetStringProp(row, "Description", "Name", "Title") ?? string.Empty;
                        var total = GetDecimalProp(row, "TotalCost", "TotalPrice", "Total");

                        sb.AppendLine($"{Csv(name)},{Csv(desc)},{total.ToString("0.##", CultureInfo.InvariantCulture)}");
                    }
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("CSV exported.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }

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
                    try { return Convert.ToDecimal(v, CultureInfo.InvariantCulture); }
                    catch { }
                }
                return 0m;
            }
        }

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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open browser.\n{ex.Message}");
            }
        }
    }
}
