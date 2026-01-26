using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ADLMRateGen.View
{
    /// <summary>
    /// Interaction logic for CarbonRateItemDetailControl.xaml
    /// </summary>
    public partial class CarbonRateItemDetailControl : UserControl
    {
        public CarbonRateItemDetailControl()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Same behavior as your other popups: just close PopupHost
            if (Application.Current?.MainWindow is MainWindow mw)
            {
                mw.PopupHost.Hide();
            }
        }
    }
}
