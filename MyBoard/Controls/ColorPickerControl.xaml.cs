using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyBoard.Controls
{
    public partial class ColorPickerControl : UserControl
    {
        
        private bool isDraggingSV;
        private bool isDraggingHue;

        public event EventHandler<string>? ColorSelected;
        public ColorPickerControl()
        {
            InitializeComponent();
            DataContext = new ColorPickerViewModel();
            DataContextChanged += ColorPickerControl_DataContextChanged;

            if (DataContext is ColorPickerViewModel vm)
                vm.ColorChanged += (s, e) => ColorSelected?.Invoke(this, vm.HexColor);
        }


        // Whenever this control is bound to a new ColorPickerViewModel (or its color changes internally), 
        // subscribe to ColorChanged so the visuals (cursor, thumb, preview, background) stay in sync automatically
        private void ColorPickerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ColorPickerViewModel oldVm)
                oldVm.ColorChanged -= ViewModel_ColorChanged;

            if (e.NewValue is ColorPickerViewModel newVm)
            {
                newVm.ColorChanged += ViewModel_ColorChanged;
                RefreshVisuals(newVm);
            }
        }

        private void ViewModel_ColorChanged(object? sender, EventArgs e)
        {
            if (DataContext is ColorPickerViewModel vm)
                RefreshVisuals(vm);
        }


        // Lets external code (like opening the popover) load an existing color into the picker, e.g. the selected board's current color
        public void LoadColor(string hex)
        {
            if (DataContext is not ColorPickerViewModel vm) return;

            vm.LoadFromHex(hex);

            Dispatcher.BeginInvoke(new Action(() => RefreshVisuals(vm)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }


        // Repositions the SV cursor, hue thumb, background hue, and preview
        // swatch to match the ViewModel's current values — called after ANY
        // change, regardless of which control originally caused it
        private void RefreshVisuals(ColorPickerViewModel vm)
        {
            var fullHueColor = HueOnlyColor(vm.Hue);
            HueBackground.Fill = new SolidColorBrush(fullHueColor);
            PreviewSwatch.Fill = new SolidColorBrush(Color.FromRgb(vm.R, vm.G, vm.B));

            if (SVSquare.ActualWidth > 0 && SVSquare.ActualHeight > 0)
            {
                double cursorX = vm.Saturation * SVSquare.ActualWidth;
                double cursorY = (1 - vm.Brightness) * SVSquare.ActualHeight;
                SVCursor.Margin = new Thickness(cursorX - SVCursor.Width / 2, cursorY - SVCursor.Height / 2, 0, 0);
            }

            if (HueSliderTrack.ActualWidth > 0)
            {
                double thumbX = (vm.Hue / 360.0) * HueSliderTrack.ActualWidth;
                HueThumb.Margin = new Thickness(thumbX - HueThumb.Width / 2, 0, 0, 0);
            }
        }


        // Returns the fully-saturated, full-brightness color for a given hue —
        // used only to tint the square's background, independent of the
        // current saturation/brightness selection
        private static Color HueOnlyColor(double hue)
        {
            double c = 1, x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            (double r, double g, double b) = hue switch
            {
                < 60 => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }


        // ---- Saturation/Brightness square dragging ----

        private void SVSquare_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingSV = true;
            SVSquare.CaptureMouse();
            UpdateSVFromMouse(e.GetPosition(SVSquare));
            e.Handled = true; // Prevents the popup's own click handling from interfering with capture
        }

        private void SVSquare_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingSV) UpdateSVFromMouse(e.GetPosition(SVSquare));
        }

        private void SVSquare_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingSV = false;
            SVSquare.ReleaseMouseCapture();
        }

        private void UpdateSVFromMouse(Point p)
        {
            if (DataContext is not ColorPickerViewModel vm) return;

            double x = Math.Clamp(p.X, 0, SVSquare.ActualWidth);
            double y = Math.Clamp(p.Y, 0, SVSquare.ActualHeight);

            vm.Saturation = x / SVSquare.ActualWidth;
            vm.Brightness = 1 - (y / SVSquare.ActualHeight);
        }


        // ---- Hue slider dragging ----

        private void HueSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingHue = true;
            HueSliderTrack.CaptureMouse();
            UpdateHueFromMouse(e.GetPosition(HueSliderTrack));
            e.Handled = true; // Same fix, same reason
        }

        private void HueSlider_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingHue) UpdateHueFromMouse(e.GetPosition(HueSliderTrack));
        }

        private void HueSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingHue = false;
            HueSliderTrack.ReleaseMouseCapture();
        }

        private void UpdateHueFromMouse(Point p)
        {
            if (DataContext is not ColorPickerViewModel vm) return;

            double x = Math.Clamp(p.X, 0, HueSliderTrack.ActualWidth);
            vm.Hue = (x / HueSliderTrack.ActualWidth) * 360.0;
        }


        // ---- RGB numeric field validation ----


        // Blocks any keystroke that isn't a digit, so the field can never
        // contain invalid characters in the first place
        private void RgbBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }


        // Clamps to 0–255 once the user finishes typing (rather than on every
        // keystroke, which would fight the user while they're still typing
        // e.g. "2" on the way to "250")
        private void RgbBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox box) return;

            if (!byte.TryParse(box.Text, out byte value))
                value = 0;

            box.Text = Math.Clamp((int)value, 0, 255).ToString();
        }



    }
    
}
