using ADLMRateGen.Command;
using ADLMRateGen.Helpers;
using ADLMRateGen.Services;
using System.Collections.Generic;
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
			IsSyncingLocation = true;
			try
			{
				var res = await MasterLibrarySyncService.SyncAsync();
				if (res.Ok)
				{
					MaterialLibraryViewModel.ReloadFromDisk();
					LabourLibraryViewModel.ReloadFromDisk();
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

		}
	}
}
