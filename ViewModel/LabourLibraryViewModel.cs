using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
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
		//private readonly JsonDataServices _dataServices;
		private const string FilePath = "labour.json";
		private const string DefaultFile = @"Data\defaultLabours.json";

		private static readonly JsonDataServices<LabourModel> _json =
	new(FilePath, DefaultFile);


		public ObservableCollection<LabourModel> LabourLibrary { get; }
		public ICollectionView LabourCollectionView { get; }

		/* --------  names for ComboBoxes  -------- */
		public static IEnumerable<string> GetAllLabourNames()
		{
			return _json.LoadData()                       // read the file
						.Select(l => l.LabourName)        // grab the name
						.Where(n => !string.IsNullOrWhiteSpace(n))
						.Distinct()
						.OrderBy(n => n);
		}

		/* --------  price lookup for RateEntryItem  -------- */
		public static decimal GetPrice(string labourName)
		{
			var labour = _json.LoadData()
							  .FirstOrDefault(l => l.LabourName == labourName);
			return labour?.LabourPrice ?? 0m;
		}


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

			LabourLibrary = new ObservableCollection<LabourModel>(_json.LoadData());
			ReassignSerialNumbers();   // keep S/N tidy

			/* 2. CollectionView for DataGrid + filter */
			LabourCollectionView = CollectionViewSource.GetDefaultView(LabourLibrary);
			LabourCollectionView.Filter = _ => true;
			ApplyFilter();


			//LabourLibrary = new ObservableCollection<LabourModel>(LabourLibraryService.GetAllLabours());
			LabourCategory = new ObservableCollection<string> { "All", "Labour", "Plant", "Small Plant" };

			SearchLabourCommand = new DelegateCommand(_ => ApplyFilter());
			ClearDatabaseCommand = new DelegateCommand(_ => ClearDatabase());
			DeleteLabourCommand = new DelegateCommand(o => DeleteLabour(o));
			EditLabourCommand = new DelegateCommand(o => EditLabour(o));
			UpdatePricesCommand = new DelegateCommand(_ => UpdatePricesFromMongo());
		}

		//private void ApplyFilter()
		//{
		//	LabourCollectionView.Filter = o =>
		//	{
		//		if (o is LabourModel labour)
		//		{
		//			bool matchesCategory = SelectedLabourCategory == "All"
		//				|| string.IsNullOrEmpty(SelectedLabourCategory)
		//				|| labour.LabourCategory == SelectedLabourCategory;

		//			bool matchesText = string.IsNullOrEmpty(SearchTerm)
		//				|| (labour.LabourName?.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0);

		//			return matchesCategory && matchesText;
		//		}
		//		return false;
		//	};
		//	LabourCollectionView.Refresh();
		//}

		/* ───────── filtering helper ───────── */
		private void ApplyFilter()
		{
			LabourCollectionView.Filter = o =>
			{
				if (o is not LabourModel lb) return false;

				var okCategory = SelectedLabourCategory == "All" ||
								 string.IsNullOrEmpty(SelectedLabourCategory) ||
								 lb.LabourCategory == SelectedLabourCategory;

				var okSearch = string.IsNullOrWhiteSpace(SearchTerm) ||
								 (lb.LabourName?.IndexOf(SearchTerm,
									StringComparison.OrdinalIgnoreCase) >= 0);

				return okCategory && okSearch;
			};
			LabourCollectionView.Refresh();
		}

		private void ClearDatabase()
		{
			LabourLibrary.Clear();
			_json.SaveData(LabourLibrary);
			ApplyFilter();
		}

		private void DeleteLabour(object o)
		{
			if (o is LabourModel labour)
			{
				LabourLibrary.Remove(labour);
				ReassignSerialNumbers();
				_json.SaveData(LabourLibrary);
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

		//public void AddOrUpdateLabour(LabourModel labour)
		//{
		//	if (labour.SerialNumber == 0)
		//	{
		//		labour.SerialNumber = LabourLibrary.Count + 1;
		//		LabourLibrary.Add(labour);
		//	}
		//	_dataServices.SaveData(LabourLibrary);
		//	LibraryChanged?.Invoke();
		//	ApplyFilter();
		//}
		public ObservableCollection<LabourModel> Labours { get; }
			= new ObservableCollection<LabourModel>();

		//public void AddOrUpdateLabour(LabourModel lab)
		//{
		//	if (lab.SerialNumber == 0)
		//		lab.SerialNumber = Labours.Count == 0
		//			? 1
		//			: Labours.Max(l => l.SerialNumber) + 1;

		//	var existing = Labours.FirstOrDefault(l => l.SerialNumber == lab.SerialNumber);
		//	if (existing == null)
		//		Labours.Add(lab);
		//	else
		//	{
		//		var idx = Labours.IndexOf(existing);
		//		Labours[idx] = lab;
		//	}
		//}

		/* ───────── CRUD helpers ───────── */
		public void AddOrUpdateLabour(LabourModel lab)
		{
			/* give a new serial if it comes in fresh */
			if (lab.SerialNumber == 0)
				lab.SerialNumber = LabourLibrary.Count == 0
								   ? 1
								   : LabourLibrary.Max(l => l.SerialNumber) + 1;

			var existing = LabourLibrary.FirstOrDefault(l => l.SerialNumber == lab.SerialNumber);

			if (existing == null)                     // *** ADD ***
			{
				LabourLibrary.Add(lab);
			}
			else                                      // *** UPDATE ***
			{
				existing.LabourUnit = lab.LabourUnit;
				existing.LabourPrice = lab.LabourPrice;
				existing.LabourCategory = lab.LabourCategory;
			}

			/* persist + refresh the grid */
			_json.SaveData(LabourLibrary);
			LabourCollectionView.Refresh();
			LibraryChanged?.Invoke();
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

			_json.SaveData(LabourLibrary);
			LibraryChanged?.Invoke();
			ApplyFilter();
			MessageBox.Show("Updated from server.");
		}

		private void OpenNewLabourDialog() =>
			MessageBox.Show("TODO: add new labour");
	}
}
