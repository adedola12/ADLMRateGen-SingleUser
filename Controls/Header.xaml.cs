using System.Windows.Controls;
using ADLMRateGen.ViewModel.Model;
using ADLMRateGen.ViewModel;
using System.Windows.Input;

namespace ADLMCivilPlugin.Controls
{
    /// <summary>
    /// Interaction logic for Header.xaml
    /// </summary>
    public partial class Header : UserControl
    {
        public Header()
        {
            InitializeComponent();
        }

		private void SuggestionList_Click(object sender, MouseButtonEventArgs e)
		{
			if (DataContext is MainViewModel vm &&
				((ListBox)sender).SelectedItem is SearchHit hit)
			{
				vm.GlobalSearch.Accept(hit);   // jump & clear list
			}
		}
	}
}
