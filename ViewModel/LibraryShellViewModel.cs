using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ADLMRateGen.View;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ADLMRateGen.ViewModel
{
	public sealed class LibraryShellViewModel : ViewModelBase
	{
		// rename these so ShowMaterialPopup can find them:
		public MaterialLibraryViewModel MaterialLibraryViewModel { get; }
		public LabourLibraryViewModel LabourLibraryViewModel { get; }

		

		// events bubble up to MainWindow so it can show the popup
		public event Action RequestAddMaterial;
		public event Action RequestAddLabour;
		public event Action<MaterialModel> RequestEditMaterial;
		public event Action<LabourModel> RequestEditLabour;
		public ICommand AddCommand { get; }

		/* ───────────────── pricing location ───────────────── */

		/// <summary>
		/// The 36 states and the FCT. Prices are graded by zone, so states inside a
		/// zone read the same until one of them is priced individually. Picking a
		/// state is still the right control to give a QS: it is what their job is
		/// in, and it is what lets their state diverge later.
		/// </summary>
		public IReadOnlyList<NigerianState> States => NigerianStates.All;

		private NigerianState _selectedState;
		public NigerianState SelectedState
		{
			get => _selectedState;
			set
			{
				if (_selectedState?.Key == value?.Key) return;
				_selectedState = value;
				RaisePropertyChanged();
				RaisePropertyChanged(nameof(LocationNote));
				if (value != null) ApplyStateChange(value);
			}
		}

		/// <summary>Shown under the picker so the zone the price actually came from is visible.</summary>
		public string LocationNote =>
			_selectedState == null
				? ""
				: $"Priced from {_selectedState.Zone.Replace('_', ' ')} rates";

		private bool _isSyncingLocation;
		public bool IsSyncingLocation
		{
			get => _isSyncingLocation;
			private set
			{
				_isSyncingLocation = value;
				RaisePropertyChanged();
				RaisePropertyChanged(nameof(IsLocationPickerEnabled));
			}
		}

		/// <summary>Inverted here rather than in XAML: there is no bool-to-bool
		/// converter in this project, only bool-to-Visibility.</summary>
		public bool IsLocationPickerEnabled => !_isSyncingLocation;

		private async void ApplyStateChange(NigerianState state)
		{
			MasterLibrarySyncService.SetState(state.Key);

			// Re-pull the master library for the new state. Without this the picker
			// would change a setting and nothing else, which is worse than not
			// offering it: the user would believe they had repriced.
			//
			// Any price the user edited themselves survives this: the sync carries
			// edits forward and parks the disagreement in PriceConflicts instead of
			// overwriting. It also archives first, so the whole change can be undone.
			IsSyncingLocation = true;
			try
			{
				var res = await MasterLibrarySyncService.SyncAsync();
				if (res.Ok)
				{
					MaterialLibraryViewModel.ReloadFromDisk();
					LabourLibraryViewModel.ReloadFromDisk();
					RefreshArchives();
				}
				else if (!string.IsNullOrWhiteSpace(res.Message))
				{
					// Sign-in required is the common case and is not an error worth a dialog.
					System.Diagnostics.Debug.WriteLine($"[Location] {res.Message}");
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					$"Could not load prices for {state.Label}.\n\n{ex.Message}",
					"Location", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
			finally
			{
				IsSyncingLocation = false;
			}
		}

		/* ───────────────── your prices vs the server's ───────────────── */

		/// <summary>
		/// Rows where a price the user typed disagrees with a price the server has
		/// since moved. The user's figure is already in force: the sync preserved
		/// it. This is an offer to switch, not a warning that something was lost.
		/// </summary>
		public IReadOnlyList<SyncBaseline.EditedRow> PriceConflicts_Rows => PriceConflicts.Pending;

		public bool HasPriceConflicts => PriceConflicts.Any;

		public string PriceConflictSummary
		{
			get
			{
				var n = PriceConflicts.Count;
				if (n == 0) return "";
				return n == 1
					? "1 rate you edited has a newer published price. Yours is being used."
					: $"{n} rates you edited have newer published prices. Yours are being used.";
			}
		}

		public ICommand UseServerPricesCommand { get; }
		public ICommand KeepMyPricesCommand { get; }

		private void RaiseConflictState()
		{
			// The sync runs off the UI thread, and Archives is an ObservableCollection
			// bound to a list, so this has to come back to the dispatcher.
			var d = Application.Current?.Dispatcher;
			if (d != null && !d.CheckAccess()) { d.Invoke(RaiseConflictState); return; }

			RaisePropertyChanged(nameof(PriceConflicts_Rows));
			RaisePropertyChanged(nameof(HasPriceConflicts));
			RaisePropertyChanged(nameof(PriceConflictSummary));
		}

		/* ───────────────── archive and undo ───────────────── */

		/// <summary>
		/// Snapshots taken before anything rewrote the library. The most recent is
		/// first, because undoing the last thing that happened is what this is for
		/// nearly every time.
		/// </summary>
		public ObservableCollection<LibraryArchive.Entry> Archives { get; } = new();

		private LibraryArchive.Entry _selectedArchive;
		public LibraryArchive.Entry SelectedArchive
		{
			get => _selectedArchive;
			set { _selectedArchive = value; RaisePropertyChanged(); }
		}

		private bool _isArchiveOpen;
		public bool IsArchiveOpen
		{
			get => _isArchiveOpen;
			set { _isArchiveOpen = value; RaisePropertyChanged(); }
		}

		public ICommand OpenArchiveCommand { get; }
		public ICommand RestoreCommand { get; }

		private void RefreshArchives()
		{
			Archives.Clear();
			foreach (var e in LibraryArchive.List()) Archives.Add(e);
			SelectedArchive = Archives.FirstOrDefault();
		}

		private void RestoreSelected()
		{
			var pick = SelectedArchive;
			if (pick == null) return;

			var ask = MessageBox.Show(
				$"Restore the material and labour libraries as they were on {pick.TakenAt:dd MMM yyyy} at {pick.TakenAt:HH:mm}?\n\n"
				+ $"{pick.MaterialRows} materials and {pick.LabourRows} labour rates will be put back.\n\n"
				+ "The current library is archived first, so this can itself be undone.",
				"Restore library", MessageBoxButton.OKCancel, MessageBoxImage.Question);
			if (ask != MessageBoxResult.OK) return;

			if (LibraryArchive.Restore(pick.Id))
			{
				MaterialLibraryViewModel.ReloadFromDisk();
				LabourLibraryViewModel.ReloadFromDisk();
				PriceConflicts.KeepMine();   // the restored figures are now the ones in force
				IsArchiveOpen = false;
				RefreshArchives();
			}
			else
			{
				MessageBox.Show("That archive could not be read, so nothing was changed.",
					"Restore library", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

		private readonly UserControl _materialView = new MaterialLibraryView();
		private readonly UserControl _labourView = new LabourLibraryView();

		private bool _isMaterialTab = true;
		public bool IsMaterialTab
		{
			get => _isMaterialTab;
			set
			{
				if (_isMaterialTab == value) return;
				_isMaterialTab = value;
				RaisePropertyChanged();
				if (value)
				{
					IsLabourTab = false;
					CurrentContent = _materialView;
					RaisePropertyChanged(nameof(AddButtonText));
				}
			}
		}

		private bool _isLabourTab;
		public bool IsLabourTab
		{
			get => _isLabourTab;
			set
			{
				if (_isLabourTab == value) return;
				_isLabourTab = value;
				RaisePropertyChanged();
				if (value)
				{
					IsMaterialTab = false;
					CurrentContent = _labourView;
					RaisePropertyChanged(nameof(AddButtonText));
				}
			}
		}

		private UserControl _currentContent;
		public UserControl CurrentContent
		{
			get => _currentContent;
			private set { _currentContent = value; RaisePropertyChanged(); }
		}

		public string AddButtonText => IsMaterialTab ? "Add Material  +" : "Add Labour  +";

		public LibraryShellViewModel(
			MaterialLibraryViewModel materialLibraryVm,
			LabourLibraryViewModel labourLibraryVm)
		{
			MaterialLibraryViewModel = materialLibraryVm;
			LabourLibraryViewModel = labourLibraryVm;

			// Seed the picker from the saved setting WITHOUT going through the
			// property, so opening the library does not fire a sync on every launch.
			_selectedState = NigerianStates.Find(MasterLibrarySyncService.ResolveState())
							 ?? NigerianStates.Find(NigerianStates.DefaultKey);

			// set the views’ data contexts
			_materialView.DataContext = MaterialLibraryViewModel;
			_labourView.DataContext = LabourLibraryViewModel;
			_currentContent = _materialView;

			AddCommand = new RelayCommand(_ =>
			{
				if (IsMaterialTab) RequestAddMaterial?.Invoke();
				else RequestAddLabour?.Invoke();     // ← **fixed**
			});

			MaterialLibraryViewModel.EditMaterialRequested += m => RequestEditMaterial?.Invoke(m);
			LabourLibraryViewModel.EditLabourRequested += l => RequestEditLabour?.Invoke(l);   // ← added

			OpenArchiveCommand = new RelayCommand(_ =>
			{
				RefreshArchives();
				IsArchiveOpen = !IsArchiveOpen;
			});

			RestoreCommand = new RelayCommand(_ => RestoreSelected());

			UseServerPricesCommand = new RelayCommand(_ =>
			{
				var rows = PriceConflicts.Pending;
				if (rows.Count == 0) return;

				var ask = MessageBox.Show(
					$"Replace your own figures on {rows.Count} rate(s) with the newly published prices?\n\n"
					+ "Your current library is archived first, so this can be undone.",
					"Use published prices", MessageBoxButton.OKCancel, MessageBoxImage.Question);
				if (ask != MessageBoxResult.OK) return;

				DataSourceCloudSync.AcceptServerPrices(rows);
				MaterialLibraryViewModel.ReloadFromDisk();
				LabourLibraryViewModel.ReloadFromDisk();
				RefreshArchives();
			});

			// Nothing to write: their prices are already the ones in use. This only
			// takes the notice down.
			KeepMyPricesCommand = new RelayCommand(_ => PriceConflicts.KeepMine());

			PriceConflicts.Changed += RaiseConflictState;
			RefreshArchives();
		}
	}
}
