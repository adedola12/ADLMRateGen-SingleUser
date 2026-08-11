using ADLMRateGen.Services;
using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.MepWork;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ADLMRateGen.View
{
    public partial class MepWorkItemDetailControl : UserControl
    {
        public event Action? BackRequested;

        public MepWorkItemDetailControl()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }

        private void QuantityTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!tb.IsEnabled) return;

            tb.IsReadOnly = false;
            tb.Focus();
            tb.SelectAll();
            e.Handled = true;
        }

        private void QuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            tb.IsReadOnly = true;

            if (tb.DataContext is not MepWorkBreakdownLine line) return;
            if (DataContext is not MepWorkItem item) return;
            if (string.IsNullOrWhiteSpace(line.ComponentName)) return;
            if (line.IsTotalLine) return;

            UserRateEditStore.Current.SetOverride(
                SectionKeys.Mep,
                item.ItemNo,
                line.ComponentName,
                line.Quantity);

            if (TryGetParentVm(out var vm))
            {
                vm!.RecomputeItemInPlace(item.ItemNo);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var ok = await RateEditCommands.SaveAsync();
            if (ok)
            {
                MessageBox.Show(
                    "Rate saved successfully. Your edits will sync to QUIV and HERON.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async void ResetThisRateButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MepWorkItem item) return;

            var confirm = MessageBox.Show(
                $"Reset all edited quantities on Item {item.ItemNo} back to the shipped defaults?\n\n" +
                "This will sync the reset to QUIV and HERON.",
                "Reset This Rate",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            await RateEditCommands.ResetItemAsync(SectionKeys.Mep, item.ItemNo);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RateEditCommands.CancelUnsavedEdits();
            BackRequested?.Invoke();
        }

        private static bool TryGetParentVm(out MepWorkViewModel? vm)
        {
            vm = null;
            if (Application.Current?.MainWindow?.DataContext is MainViewModel main)
            {
                vm = main.MepWorkViewModel;
                return vm != null;
            }
            return false;
        }
    }
}
