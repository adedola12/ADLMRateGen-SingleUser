using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.Model;
using FontAwesome.Sharp;

namespace ADLMCivilPlugin.Controls
{
    public partial class Header : UserControl
    {
        public Header()
        {
            InitializeComponent();
        }

        private void Header_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateThemeIcon((Application.Current as ADLMRateGen.App)?.IsDarkTheme == true);
        }

        private void SuggestionList_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm &&
                ((ListBox)sender).SelectedItem is SearchHit hit)
            {
                vm.GlobalSearch.Accept(hit);
            }
        }

        private void ColorModeButton_Click(object sender, RoutedEventArgs e)
        {
            var isDark = (Application.Current as ADLMRateGen.App)?.ToggleTheme() == true;
            UpdateThemeIcon(isDark);
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://adlmstudio.net/profile")
                {
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to open profile.\n{ex.Message}");
            }
        }

        private void UpdateThemeIcon(bool isDark)
        {
            ColorModeIcon.Icon = isDark ? IconChar.Sun : IconChar.Moon;
            ColorModeButton.ToolTip = isDark ? "Switch to light mode" : "Switch to dark mode";
        }
    }
}
