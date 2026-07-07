using System.Windows;
using System.Windows.Controls;

namespace ParkinsonAppWindows.Controls
{
    public partial class LegendControl : UserControl
    {
        public LegendControl()
        {
            InitializeComponent();
        }

        private void OnToggleClick(object sender, RoutedEventArgs e)
        {
            if (ExpandToggle.IsChecked == true)
            {
                ContentPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}