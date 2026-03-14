using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ADLMRateGen.Helpers
{
    public static class DataGridScrollHelper
    {
        public static readonly DependencyProperty EnableMouseWheelScrollProperty =
            DependencyProperty.RegisterAttached(
                "EnableMouseWheelScroll",
                typeof(bool),
                typeof(DataGridScrollHelper),
                new PropertyMetadata(false, OnEnableMouseWheelScrollChanged));

        public static bool GetEnableMouseWheelScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableMouseWheelScrollProperty);
        }

        public static void SetEnableMouseWheelScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableMouseWheelScrollProperty, value);
        }

        private static void OnEnableMouseWheelScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                element.PreviewMouseWheel += OnPreviewMouseWheel;
            }
            else
            {
                element.PreviewMouseWheel -= OnPreviewMouseWheel;
            }
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            var scrollViewer = FindDescendant<ScrollViewer>(dataGrid);
            if (scrollViewer == null || scrollViewer.ScrollableHeight <= 0)
            {
                return;
            }

            var isScrollingUp = e.Delta > 0;
            var canScroll = isScrollingUp
                ? scrollViewer.VerticalOffset > 0
                : scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;

            if (!canScroll)
            {
                return;
            }

            var linesToScroll = SystemParameters.WheelScrollLines <= 0
                ? 3
                : SystemParameters.WheelScrollLines;

            for (var i = 0; i < linesToScroll; i++)
            {
                if (isScrollingUp)
                {
                    scrollViewer.LineUp();
                }
                else
                {
                    scrollViewer.LineDown();
                }
            }

            e.Handled = true;
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var nestedChild = FindDescendant<T>(child);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }
    }
}
