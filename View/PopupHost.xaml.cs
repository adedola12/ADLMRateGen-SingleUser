using System.Windows;
using System.Windows.Controls;

namespace ADLMRateGen.View
{
	public partial class PopupHost : UserControl
	{
		public PopupHost() => InitializeComponent();

		/// <summary>
		/// Places the given UserControl into this popup and shows it.
		/// </summary>
		public void Show(UserControl view)
		{
			// second child of the Grid inside the Border is our ContentPresenter
			((ContentPresenter)((Grid)Card.Child).Children[1]).Content = view;
			Visibility = Visibility.Visible;
		}

		/// <summary>Hides the popup.</summary>
		public void Hide() => Visibility = Visibility.Collapsed;

		private void OnClose(object sender, RoutedEventArgs e) => Hide();

		private void OnCloseClick(object sender, RoutedEventArgs e)
		{
			this.Visibility = Visibility.Collapsed;
		}
	}
}
