using System.Windows;
using System.Windows.Controls;

namespace MyBoard.Controls
{
    // A self-contained "Color" tool: button + swatch + popover, reusable for
    // any selectable item type. Owns its own popup positioning fix, so no future design-panel tool needs to re-solve the RenderTransform problem.
    public partial class ColorPickerButton : UserControl
    {

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(nameof(SelectedColor), typeof(string), typeof(ColorPickerButton),
                new PropertyMetadata("#B39DDB"));


        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ColorPickerButton),
                new PropertyMetadata(false));


        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }
        public string SelectedColor
        {
            get => (string)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        // Fires on every live change while dragging in the picker
        public event EventHandler<string>? ColorSelected;

        // Fire when the popover opens/closes
        public event EventHandler? PopoverOpened;
        public event EventHandler? PopoverClosed;

        public ColorPickerButton()
        {
            InitializeComponent();
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ColorPopup.IsOpen = !ColorPopup.IsOpen;
        }

        // Manually calculates the popup's true screen position using
        // PointToScreen, which correctly accounts for any RenderTransform
        // on ancestors (unlike Popup's automatic Placement modes)
        private void ColorPopup_Opened(object sender, EventArgs e)
        {
            Point screenPoint = ToggleButton.PointToScreen(
                new Point(ToggleButton.ActualWidth + 8, 0));

            ColorPopup.HorizontalOffset = screenPoint.X;
            ColorPopup.VerticalOffset = screenPoint.Y;

            InnerColorPicker.LoadColor(SelectedColor);
            PopoverOpened?.Invoke(this, EventArgs.Empty);
        }

        private void ColorPopup_Closed(object sender, EventArgs e)
        {
            PopoverClosed?.Invoke(this, EventArgs.Empty);
        }

        private void InnerColorPicker_ColorSelected(object? sender, string hex)
        {
            SelectedColor = hex;
            ColorSelected?.Invoke(this, hex);
        }

        // Lets external code (like an Escape key handler) force the popover closed
        public void ClosePopover() => ColorPopup.IsOpen = false;
    }
}