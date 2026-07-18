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
        public void ClosePopover()
        {
            System.Diagnostics.Debug.WriteLine("ClosePopover called. Stack trace:\n" + Environment.StackTrace);
            StylePopup.IsOpen = false;
        }


        public TextStylePicker()
        {
            InitializeComponent();
            StylePopup.Closed += (s, e) => PopoverClosed?.Invoke(this, EventArgs.Empty);

            Loaded += (_, _) =>
                System.Diagnostics.Debug.WriteLine("TextStylePicker Loaded");

            Unloaded += (_, _) =>
                System.Diagnostics.Debug.WriteLine("TextStylePicker Unloaded");

            StylePopup.Opened += (s, e) =>
                System.Diagnostics.Debug.WriteLine("Popup Opened");

            StylePopup.Closed += (s, e) =>
                System.Diagnostics.Debug.WriteLine("Popup Closed");
        }


        public event EventHandler? AboutToOpen;

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!StylePopup.IsOpen)
                AboutToOpen?.Invoke(this, EventArgs.Empty);

            StylePopup.IsOpen = !StylePopup.IsOpen;
        }


        public Point AnchorScreenPoint { get; set; }

        private void StylePopup_Opened(object sender, EventArgs e)
        {
            StylePopup.HorizontalOffset = AnchorScreenPoint.X;
            StylePopup.VerticalOffset = AnchorScreenPoint.Y;
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
        }
    }
}