using MyBoard.Commands;
using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MyBoard
{
    public partial class MainWindow
    {
        private const double SidebarPanelWidth = 180;
        private string colorBeforeEdit = "";

        public static readonly DependencyProperty IsSidebarExpandedProperty =
            DependencyProperty.Register(nameof(IsSidebarExpanded), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));


        public bool IsSidebarExpanded
        {
            get => (bool)GetValue(IsSidebarExpandedProperty);
            set => SetValue(IsSidebarExpandedProperty, value);
        }


        private void SlideSidebarPanel(bool showDesignPanel)
        {
            var animation = new DoubleAnimation
            {
                To = showDesignPanel ? -SidebarPanelWidth : 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            SidebarSlideTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }


        // Captures the color at the moment editing begins, so we can record
        // a single undo step for the whole session rather than one per drag-tick
        private void DesignColorPicker_PopoverOpened(object? sender, EventArgs e)
        {
            isColorPopoverOpen = true;
            IsSidebarExpanded = true;
            AnimateSidebarWidth(180);

            if (((MainViewModel)DataContext).CurrentBoard.PrimarySelectedItem is BoardViewModel board)
                colorBeforeEdit = board.Color;
        }

        private void DesignColorPicker_PopoverClosed(object? sender, EventArgs e)
        {
            isColorPopoverOpen = false;

            if (!Sidebar.IsMouseOver)
            {
                IsSidebarExpanded = false;
                AnimateSidebarWidth(60);
            }

            var viewModel = (MainViewModel)DataContext;
            if (viewModel.CurrentBoard.PrimarySelectedItem is BoardViewModel board && board.Color != colorBeforeEdit)
                viewModel.UndoRedo.Record(new ColorChangeCommand(board, colorBeforeEdit, board.Color));
        }

        // Live updates while dragging — SelectedColor's TwoWay binding to
        // board.Color already applies this instantly, so nothing extra
        // needs to happen here beyond what the binding does automatically
        private void DesignColorPicker_ColorSelected(object? sender, string hex) { }


    }
}