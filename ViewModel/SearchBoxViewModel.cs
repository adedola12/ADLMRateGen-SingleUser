using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;

namespace ADLMRateGen.ViewModel
{
	public class SearchBoxViewModel : INotifyPropertyChanged
	{
		private readonly SearchIndex _index;

		public SearchBoxViewModel(SearchIndex index) => _index = index;

		private string _text = "";
		public string Text
		{
			get => _text;
			set
			{
				if (_text == value) return;
				_text = value;
				OnPropertyChanged();
				UpdateSuggestions();
			}
		}

		public ObservableCollection<SearchHit> Suggestions { get; } = new();

		private void UpdateSuggestions()
		{
			Suggestions.Clear();
			if (string.IsNullOrWhiteSpace(_text)) return;

			foreach (var hit in _index.Hits
									   .Where(h => h.Display.Contains(_text,
												  StringComparison.OrdinalIgnoreCase))
									   .Take(10))
				Suggestions.Add(hit);
		}

		public void Accept(SearchHit hit)
		{
			Text = "";
			Suggestions.Clear();
			hit.Navigate();          // jump to page + highlight
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string? p = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
