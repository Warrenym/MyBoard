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


        private ViewModel.ColorPaletteViewModel? cachedPalette;
        private string? colorToRecord;


        private void ColorPopup_Opened(object sender, EventArgs e)
        {
            Point screenPoint = ToggleButton.PointToScreen(new Point(ToggleButton.ActualWidth + 8, 0));
            ColorPopup.HorizontalOffset = screenPoint.X;
            ColorPopup.VerticalOffset = screenPoint.Y;

            InnerColorPicker.LoadColor(SelectedColor);
            colorCameFromCustomPicker = false;

            cachedPalette = Palette as ViewModel.ColorPaletteViewModel;

            PopoverOpened?.Invoke(this, EventArgs.Empty);
        }

        private void ColorPopup_Closed(object sender, EventArgs e)
        {
            if (colorCameFromCustomPicker && colorToRecord != null && cachedPalette != null)
                cachedPalette.RecordRecentlyPicked(colorToRecord);

            PopoverClosed?.Invoke(this, EventArgs.Empty);
        }

        private void InnerColorPicker_ColorSelected(object? sender, string hex)
        {
            SelectedColor = hex;
            colorCameFromCustomPicker = true;
            colorToRecord = hex; // Cache the actual dragged color too, for the same reason
            ColorSelected?.Invoke(this, hex);
        }

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