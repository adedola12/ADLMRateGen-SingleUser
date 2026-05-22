using ADLMRateGen.Services;
using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.ConcreteWork;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for ConcreteworkItemDetailControl.xaml.
    /// Hosts the editable rate build-up popup for Concrete Works.
    /// </summary>
    public partial class ConcreteworkItemDetailControl : UserControl
    {
        public event Action? BackRequested;

        public ConcreteworkItemDetailControl()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }

        /// <summary>
        /// Double-click on a Quantity cell unlocks it for editing. We flip IsReadOnly off,
        /// focus the box, and select all so the user can immediately type a replacement value.
        /// The Style trigger paints the box yellow + border to signal edit mode.
        /// </summary>
        private void QuantityTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (!tb.IsEnabled) return;

            tb.IsReadOnly = false;
            tb.Focus();
            tb.SelectAll();
            e.Handled = true;
        }

        /// <summary>
        /// Fires when the user finishes editing a Quantity cell. Writes the new value to the
        /// override store, then asks the parent ConcreteViewModel to recompute this item in place
        /// so the sub-totals refresh immediately in the popup. Also reverts the box to read-only
        /// display mode.
        /// </summary>
        private void QuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            tb.IsReadOnly = true; // back to display mode

            if (tb.DataContext is not ConcreteworkBreakdownLine line) return;
            if (DataContext is not ConcreteworkItem item) return;
            if (string.IsNullOrWhiteSpace(line.ComponentName)) return;
            if (line.IsTotalLine) return; // belt-and-braces

            UserRateEditStore.Current.SetOverride(
                SectionKeys.Concrete,
                item.ItemNo,
                line.ComponentName,
                line.Quantity);

            // Live recompute so sub-totals + NetCost + Total update in the open popup.
            // (RecomputeItemInPlace is also fired indirectly via the OverridesChanged
            // subscription, but calling it here ensures the popup refreshes even if some
            // future change suppresses that path.)
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
            if (DataContext is not ConcreteworkItem item) return;

            var confirm = MessageBox.Show(
                $"Reset all edited quantities on Item {item.ItemNo} back to the shipped defaults?\n\n" +
                "This will sync the reset to QUIV and HERON.",
                "Reset This Rate",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            await RateEditCommands.ResetItemAsync(SectionKeys.Concrete, item.ItemNo);
            // OverridesChanged fires inside ResetItemAsync → in-place recompute happens
            // for every item in the section. Popup re-renders automatically.
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RateEditCommands.CancelUnsavedEdits();
            // OverridesChanged fires inside CancelUnsavedEdits → in-place recompute.
            BackRequested?.Invoke();
        }

        private static bool TryGetParentVm(out ConcreteViewModel? vm)
        {
            vm = null;
            if (Application.Current?.MainWindow?.DataContext is MainViewModel main)
            {
                vm = main.ConcreteViewModel;
                return vm != null;
            }
            return false;
        }
    }
}
