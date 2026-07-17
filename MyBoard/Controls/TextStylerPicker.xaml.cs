using MyBoard.Model;
using MyBoard.Model;
using System.Windows;
using System.Windows.Controls;

namespace MyBoard.Controls
{
    public partial class TextStylePicker : UserControl
    {
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TextStylePicker),
                new PropertyMetadata(false));
        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        // The RichTextBox this picker acts on — set by MainWindow when a
        // note is selected, so this control stays generic and doesn't need
        // to know about NoteItemViewModel directly
        public RichTextBox? TargetRichTextBox { get; set; }

        public event EventHandler? PopoverOpened;
        public event EventHandler? PopoverClosed;

        public TextStylePicker()
        {
            InitializeComponent();
            StylePopup.Closed += (s, e) => PopoverClosed?.Invoke(this, EventArgs.Empty);
        }


        private void ToggleButton_Click(object sender, RoutedEventArgs e) => StylePopup.IsOpen = !StylePopup.IsOpen;

        private void StylePopup_Opened(object sender, EventArgs e)
        {
            Point screenPoint = ToggleButton.PointToScreen(new Point(ToggleButton.ActualWidth + 8, 0));
            StylePopup.HorizontalOffset = screenPoint.X;
            StylePopup.VerticalOffset = screenPoint.Y;
            PopoverOpened?.Invoke(this, EventArgs.Empty);
        }


        private void StyleOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.Tag is not string tagName) return;
            if (TargetRichTextBox == null) return;
            if (!Enum.TryParse<Model.NoteBlockType>(tagName, out var blockType)) return;

            Services.NoteDocumentConverter.ApplyBlockType(TargetRichTextBox, blockType);

            // Push the change back to the ViewModel immediately — not just on
            // edit-exit — so ShowPlaceholder (and anything else bound to Document)
            // reflects the new style right away, even if the note isn't actively
            // being edited when the style is applied
            if (TargetRichTextBox.DataContext is ViewModel.NoteItemViewModel note)
                note.Document = Services.NoteDocumentConverter.ToNoteDocument(TargetRichTextBox.Document);

            TargetRichTextBox.Focus();
            StylePopup.IsOpen = false;
        }
    }
}