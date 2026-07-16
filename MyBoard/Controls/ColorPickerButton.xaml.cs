using System.Windows;
using System.Windows.Controls;

namespace MyBoard.Controls
{
    public partial class ColorPickerButton : UserControl
    {
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(nameof(SelectedColor), typeof(string), typeof(ColorPickerButton),
                new PropertyMetadata("#B39DDB"));


        public string SelectedColor
        {
            get => (string)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }


        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ColorPickerButton),
                new PropertyMetadata(false));


        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }


        // The shared app-wide palette — set once by whoever hosts this control
        // (MainWindow, bound to MainViewModel.ColorPalette)
        public static readonly DependencyProperty PaletteProperty =
            DependencyProperty.Register(nameof(Palette), typeof(object), typeof(ColorPickerButton));


        public object Palette
        {
            get => GetValue(PaletteProperty);
            set => SetValue(PaletteProperty, value);
        }


        public event EventHandler<string>? ColorSelected;
        public event EventHandler? PopoverOpened;
        public event EventHandler? PopoverClosed;

        // Tracks whether the current SelectedColor came from dragging in the
        // custom picker (vs clicking a preset swatch) — only custom-picked
        // colors get added to Recently Picked when the popover closes
        private bool colorCameFromCustomPicker;

        public ColorPickerButton()
        {
            InitializeComponent();
        }


        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ColorPopup.IsOpen = !ColorPopup.IsOpen;
        }

        private void ColorPopup_Opened(object sender, EventArgs e)
        {
            Point screenPoint = ToggleButton.PointToScreen(new Point(ToggleButton.ActualWidth + 8, 0));
            ColorPopup.HorizontalOffset = screenPoint.X;
            ColorPopup.VerticalOffset = screenPoint.Y;

            InnerColorPicker.LoadColor(SelectedColor);
            colorCameFromCustomPicker = false;
            PopoverOpened?.Invoke(this, EventArgs.Empty);
        }


        private void ColorPopup_Closed(object sender, EventArgs e)
        {
            if (colorCameFromCustomPicker && Palette is ViewModel.ColorPaletteViewModel palette)
                palette.RecordRecentlyPicked(SelectedColor);

            PopoverClosed?.Invoke(this, EventArgs.Empty);
        }


        private void InnerColorPicker_ColorSelected(object? sender, string hex)
        {
            SelectedColor = hex;
            colorCameFromCustomPicker = true;
            ColorSelected?.Invoke(this, hex);
        }


        // A palette swatch was clicked — apply it immediately, sync the
        // custom picker's visuals to match, but don't mark it as "from custom
        // picker" since it's already sitting in a palette row
        private void PaletteRows_SwatchClicked(object? sender, (string Hex, string Row) e)
        {
            SelectedColor = e.Hex;
            InnerColorPicker.LoadColor(e.Hex);
            colorCameFromCustomPicker = false;
            ColorSelected?.Invoke(this, e.Hex);
        }

        public void ClosePopover() => ColorPopup.IsOpen = false;
    }
}