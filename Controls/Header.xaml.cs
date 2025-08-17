using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FontAwesome.Sharp;
using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.Model;

namespace ADLMCivilPlugin.Controls
{
	public partial class Header : UserControl
	{
		public Header() => InitializeComponent();

		private void SuggestionList_Click(object sender, MouseButtonEventArgs e)
		{
			if (DataContext is MainViewModel vm &&
				((ListBox)sender).SelectedItem is SearchHit hit)
			{
				vm.GlobalSearch.Accept(hit);
			}
		}

		//private void ColorModeButton_Click(object sender, RoutedEventArgs e)
		//{
		//	(Application.Current as ADLMRateGen.App)?.ToggleTheme();

		//	ColorModeIcon.Icon = (ColorModeIcon.Icon == IconChar.Moon)
		//						   ? IconChar.Sun
		//						   : IconChar.Moon;
		//}
	}
}
