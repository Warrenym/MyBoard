using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyBoard.ViewModel;

namespace MyBoard.Controls
{
    public partial class ColorPaletteRows : UserControl
    {
        public event EventHandler<(string Hex, string Row)>? SwatchClicked;

        public static readonly DependencyProperty IsEditModeProperty =
            DependencyProperty.Register(nameof(IsEditMode), typeof(bool), typeof(ColorPaletteRows),
                new PropertyMetadata(false));
        public bool IsEditMode
        {
            get => (bool)GetValue(IsEditModeProperty);
            set => SetValue(IsEditModeProperty, value);
        }

        public static readonly DependencyProperty CurrentColorProperty =
            DependencyProperty.Register(nameof(CurrentColor), typeof(string), typeof(ColorPaletteRows),
                new PropertyMetadata("#B39DDB"));
        public string CurrentColor
        {
            get => (string)GetValue(CurrentColorProperty);
            set => SetValue(CurrentColorProperty, value);
        }

        private ColorPaletteViewModel? Palette => DataContext as ColorPaletteViewModel;

        // Drag-start tracking — WPF requires manually detecting "moved far
        // enough to count as a drag" rather than getting this for free
        private Point dragStartPoint;
        private bool isPotentialDrag;

        private const string DragFormat = "PaletteColorDrag";

        public ColorPaletteRows()
        {
            InitializeComponent();
        }

        private void Swatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsEditMode)
            {
                // In edit mode, a click doesn't apply the color — instead,
                // remember where the press started, in case this becomes a drag
                dragStartPoint = e.GetPosition(null);
                isPotentialDrag = true;
                e.Handled = true;
                return;
            }

            if (sender is FrameworkElement el && el.DataContext is string hex)
            {
                string row = el.Tag as string ?? "";
                SwatchClicked?.Invoke(this, (hex, row));
                e.Handled = true;
            }
        }

        private void Swatch_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPotentialDrag || e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not FrameworkElement el || el.DataContext is not string hex) return;

            Point current = e.GetPosition(null);
            // Only start an actual drag once the mouse has moved past the OS's
            // configured drag threshold — prevents a simple click-in-edit-mode
            // from accidentally triggering a drag
            if (Math.Abs(current.X - dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            isPotentialDrag = false;
            string row = el.Tag as string ?? "";

            var data = new DataObject(DragFormat, (hex, row));
            DragDrop.DoDragDrop(el, data, DragDropEffects.Move);
        }

        private void DeleteButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not string hex) return;
            string row = el.Tag as string ?? "";

            if (row == "Saved") Palette?.RemoveFromSaved(hex);
            else if (row == "Recent") Palette?.RemoveFromRecentlyPicked(hex);

            e.Handled = true;
        }

        private void AddSavedButton_Click(object sender, RoutedEventArgs e) => Palette?.AddToSaved(CurrentColor);

        private void SaveFromRecent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.CommandParameter is string hex)
                Palette?.AddToSaved(hex);
        }

        // Shared Drop handler for both the Saved and Recently Picked ItemsControls —
        // which row it targets is read from the ItemsControl's own Tag
        private void Row_Drop(object sender, DragEventArgs e)
        {
            if (sender is not ItemsControl targetControl) return;
            if (!e.Data.GetDataPresent(DragFormat)) return;

            var (hex, sourceRowName) = ((string, string))e.Data.GetData(DragFormat);
            string targetRowName = targetControl.Tag as string ?? "";

            var sourceRow = ParseRow(sourceRowName);
            var targetRow = ParseRow(targetRowName);

            if (sourceRow == targetRow)
            {
                int index = GetDropIndex(targetControl, e.GetPosition(targetControl));
                Palette?.ReorderWithinRow(targetRow, hex, index);
            }
            else
            {
                Palette?.TryMoveColor(hex, sourceRow, targetRow);
            }

            e.Handled = true;
        }

        private static PaletteRow ParseRow(string name) => name switch
        {
            "Default" => PaletteRow.Default,
            "Saved" => PaletteRow.Saved,
            "Recent" => PaletteRow.Recent,
            _ => PaletteRow.Recent
        };

        // Figures out where in the row a drop should insert, by comparing the
        // drop point against each existing swatch's on-screen bounds
        private static int GetDropIndex(ItemsControl itemsControl, Point dropPoint)
        {
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
                    continue;

                Point topLeft = container.TransformToAncestor(itemsControl).Transform(new Point(0, 0));
                Rect bounds = new(topLeft, new Size(container.ActualWidth, container.ActualHeight));

                if (dropPoint.X < bounds.X + bounds.Width / 2 &&
                    dropPoint.Y >= bounds.Y && dropPoint.Y <= bounds.Y + bounds.Height)
                    return i;
            }
            return itemsControl.Items.Count; // Dropped past the last item — append to the end
        }
    }
}