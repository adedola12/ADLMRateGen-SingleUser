using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ADLMRateGen.Command;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
	public class LabourLibraryViewModel : ViewModelBase
	{
		private readonly JsonDataServices _dataServices;
		private const string FilePath = "labour.json";
		private const string DefaultFilePath = "Data\\defaultLabours.json";

		public ObservableCollection<LabourModel> LabourLibrary { get; }
		public ICollectionView LabourCollectionView { get; }
		public ObservableCollection<string> LabourCategory { get; }

		private string _selectedLabourCategory = "All";
		public string SelectedLabourCategory
		{
			get => _selectedLabourCategory;
			set
			{
				if (_selectedLabourCategory != value)
				{
					_selectedLabourCategory = value;
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

		public ICommand SearchLabourCommand { get; }
		public ICommand ClearDatabaseCommand { get; }
		public ICommand DeleteLabourCommand { get; }
		public ICommand EditLabourCommand { get; }
		public ICommand UpdatePricesCommand { get; }

		// Fired when the user clicks “Edit” on a row
		public event Action<LabourModel> EditLabourRequested;
		public event Action LibraryChanged;

		public LabourLibraryViewModel()
		{
			_dataServices = new JsonDataServices(FilePath, DefaultFilePath);

			// initialize data source (JSON or Mongo)...
			LabourLibraryService.Initialize(new LabourJsonDataSource(FilePath));

			LabourLibrary = new ObservableCollection<LabourModel>(LabourLibraryService.GetAllLabours());
			LabourCollectionView = CollectionViewSource.GetDefaultView(LabourLibrary);
			LabourCategory = new ObservableCollection<string> { "All", "Labour", "Plant", "Small Plant" };

			SearchLabourCommand = new DelegateCommand(_ => ApplyFilter());
			ClearDatabaseCommand = new DelegateCommand(_ => ClearDatabase());
			DeleteLabourCommand = new DelegateCommand(o => DeleteLabour(o));
			EditLabourCommand = new DelegateCommand(o => EditLabour(o));
			UpdatePricesCommand = new DelegateCommand(_ => UpdatePricesFromMongo());
		}

		private void ApplyFilter()
		{
			LabourCollectionView.Filter = o =>
			{
				if (o is LabourModel labour)
				{
					bool matchesCategory = SelectedLabourCategory == "All"
						|| string.IsNullOrEmpty(SelectedLabourCategory)
						|| labour.LabourCategory == SelectedLabourCategory;

					bool matchesText = string.IsNullOrEmpty(SearchTerm)
						|| (labour.LabourName?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0);

					return matchesCategory && matchesText;
				}
				return false;
			};
			LabourCollectionView.Refresh();
		}

		private void ClearDatabase()
		{
			LabourLibrary.Clear();
			_dataServices.SaveData(LabourLibrary);
			ApplyFilter();
		}

		private void DeleteLabour(object o)
		{
			if (o is LabourModel labour)
			{
				LabourLibrary.Remove(labour);
				ReassignSerialNumbers();
				_dataServices.SaveData(LabourLibrary);
				LibraryChanged?.Invoke();
				ApplyFilter();
			}
		}

		private void ReassignSerialNumbers()
		{
			for (int i = 0; i < LabourLibrary.Count; i++)
				LabourLibrary[i].SerialNumber = i + 1;
		}

		private void EditLabour(object o)
		{
			if (o is LabourModel labour)
				EditLabourRequested?.Invoke(labour);
		}

		public void AddOrUpdateLabour(LabourModel labour)
		{
			if (labour.SerialNumber == 0)
			{
				labour.SerialNumber = LabourLibrary.Count + 1;
				LabourLibrary.Add(labour);
			}
			_dataServices.SaveData(LabourLibrary);
			LibraryChanged?.Invoke();
			ApplyFilter();
		}

		private void UpdatePricesFromMongo()
		{
			var result = MessageBox.Show(
				"Override prices with ADLM server values?",
				"Confirm",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			if (result != MessageBoxResult.Yes)
				return;

			var mongo = new LabourMongoDataSource(
				"mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
				"ADLMRateDB",
				"labours"
			);
			var serverList = mongo.LoadLabours().ToList();

			foreach (var local in LabourLibrary)
			{
				var found = serverList.FirstOrDefault(s => s.LabourName == local.LabourName);
				if (found != null)
					local.LabourPrice = found.LabourPrice;
			}

			_dataServices.SaveData(LabourLibrary);
			LibraryChanged?.Invoke();
			ApplyFilter();
			MessageBox.Show("Updated from server.");
		}

		private void OpenNewLabourDialog() =>
			MessageBox.Show("TODO: add new labour");
	}
}
