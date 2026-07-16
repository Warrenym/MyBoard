using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyBoard.Controls
{
    // Displays the three palette rows (Default/Saved/Recent) and reports clicks upward. 
    // Deliberately knows nothing about HSV math or undo — just "here are some colors, tell the parent which one got clicked."
    public partial class ColorPaletteRows : UserControl
    {
        // Carries both the color AND which row it came from
        public event EventHandler<(string Hex, string Row)>? SwatchClicked;

        public ColorPaletteRows()
        {
            InitializeComponent();
        }

        private void Swatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is string hex)
            {
                string row = el.Tag as string ?? "";
                SwatchClicked?.Invoke(this, (hex, row));
                e.Handled = true;
            }
        }
    }
}