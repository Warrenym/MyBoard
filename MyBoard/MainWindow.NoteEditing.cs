using MyBoard.Model;
using MyBoard.Services;
using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace MyBoard
{
    public partial class MainWindow
    {
        private void NoteDesignTool_PopoverOpened(object? sender, EventArgs e)
        {
            isColorPopoverOpen = true; // reuse the same flag — any design popover keeps the sidebar open
            IsSidebarExpanded = true;
            AnimateSidebarWidth(180);

            if (sender is Controls.TextStylePicker picker &&
                ((MainViewModel)DataContext).CurrentBoard.PrimarySelectedItem is NoteItemViewModel note)
            {
                // Find the currently-selected note's RichTextBox in the visual tree
                picker.TargetRichTextBox = FindNoteRichTextBox(note);
            }
        }


        private void NoteDesignTool_PopoverClosed(object? sender, EventArgs e)
        {
            isColorPopoverOpen = false;
            if (!Sidebar.IsMouseOver)
            {
                IsSidebarExpanded = false;
                AnimateSidebarWidth(60);
            }
        }


        // Locates the on-canvas RichTextBox belonging to a specific note ViewModel —
        // needed since the sidebar's TextStylePicker lives in a different part of
        // the visual tree than the note itself
        private RichTextBox? FindNoteRichTextBox(NoteItemViewModel note)
        {
            if (BoardCanvas.ItemContainerGenerator.ContainerFromItem(note) is not FrameworkElement container)
                return null;

            return FindVisualChild<RichTextBox>(container);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }


        // Fires once when a note's RichTextBox first enters the visual tree —
        // loads its initial content, then subscribes to IsEditing so entering/
        // exiting edit mode loads/saves the document at the right moments.
        private void NoteRichTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not RichTextBox rtb) return;
            if (rtb.DataContext is not NoteItemViewModel note) return;

            rtb.Document = NoteDocumentConverter.ToFlowDocument(note.Document);

            note.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName != nameof(NoteItemViewModel.IsEditing)) return;

                if (note.IsEditing)
                {
                    rtb.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        rtb.Focus();
                        rtb.CaretPosition = rtb.Document.ContentEnd;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    // Leaving edit mode — convert the FlowDocument back into
                    // our portable model and save it to the ViewModel/Model
                    note.Document = NoteDocumentConverter.ToNoteDocument(rtb.Document);
                }
            };
        }


        // Escape exits edit mode, matching the old TextBox behavior
        private void NoteRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not RichTextBox rtb) return;
            var paragraph = rtb.CaretPosition.Paragraph;
            if (paragraph == null) return;
            if (Services.NoteDocumentConverter.DetectBlockTypePublic(paragraph) != Model.NoteBlockType.QuoteBlock) return;


            if (rtb.CaretPosition.CompareTo(paragraph.ContentStart) <= 0)
                rtb.CaretPosition = paragraph.ContentStart.GetPositionAtOffset(1) ?? paragraph.ContentStart;
            else if (rtb.CaretPosition.CompareTo(paragraph.ContentEnd) >= 0)
                rtb.CaretPosition = paragraph.ContentEnd.GetPositionAtOffset(-1) ?? paragraph.ContentEnd;


            if (e.Key == Key.Escape && rtb.DataContext is ViewModel.NoteItemViewModel note)
            {
                note.IsEditing = false;
                Keyboard.Focus(RootGrid);
                e.Handled = true;
                return;
            }

            // Shift+Enter: let WPF's default line-break behavior happen untouched —
            // this preserves the current block's style, per your spec
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                return;

            // Plain Enter: if the CURRENT line isn't Normal, force the new line
            // (which WPF is about to create) back to Normal once it exists
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                var currentParagraph = rtb.CaretPosition.Paragraph;
                bool wasNonNormal = currentParagraph != null &&
                    Services.NoteDocumentConverter.DetectBlockTypePublic(currentParagraph) != Model.NoteBlockType.Normal;

                if (wasNonNormal)
                {
                    // Let WPF handle the actual Enter/new-paragraph creation first,
                    // then reset the NEW paragraph's style on the next dispatcher tick
                    // (the new Paragraph doesn't exist yet at the moment KeyDown fires)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var newParagraph = rtb.CaretPosition.Paragraph;
                        if (newParagraph != null)
                            Services.NoteDocumentConverter.ApplyBlockType(rtb, Model.NoteBlockType.Normal);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        private static (TextPointer? min, TextPointer? max) GetQuoteBoundaries(Paragraph paragraph)
        {
            if (paragraph.Inlines.FirstInline is InlineUIContainer openContainer &&
                paragraph.Inlines.LastInline is InlineUIContainer closeContainer &&
                openContainer != closeContainer)
            {
                return (openContainer.ElementEnd, closeContainer.ElementStart);
            }
            return (null, null);
        }

        private void NoteRichTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not RichTextBox rtb) return;
            var paragraph = rtb.CaretPosition.Paragraph;
            if (paragraph == null) return;
            if (Services.NoteDocumentConverter.DetectBlockTypePublic(paragraph) != Model.NoteBlockType.QuoteBlock) return;

            var (min, max) = GetQuoteBoundaries(paragraph);
            if (min == null || max == null) return;

            if (rtb.CaretPosition.CompareTo(min) < 0) rtb.CaretPosition = min;
            else if (rtb.CaretPosition.CompareTo(max) > 0) rtb.CaretPosition = max;
        }


        // Runs every time the caret moves (click, arrow keys, etc.) —
        //  clamps it back inside the quote marks immediately, before the user has a chance to type at an invalid position
        private bool isAdjustingCaret;

        private void NoteRichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (isAdjustingCaret) return;
            if (sender is not RichTextBox rtb) return;

            var paragraph = rtb.CaretPosition.Paragraph;
            if (paragraph == null) return;
            if (Services.NoteDocumentConverter.DetectBlockTypePublic(paragraph) != Model.NoteBlockType.QuoteBlock) return;

            var (min, max) = GetQuoteBoundaries(paragraph);
            if (min == null || max == null) return;

            TextPointer? target = null;
            if (rtb.CaretPosition.CompareTo(min) < 0) target = min;
            else if (rtb.CaretPosition.CompareTo(max) > 0) target = max;

            if (target != null && target.CompareTo(rtb.CaretPosition) != 0)
            {
                isAdjustingCaret = true;
                rtb.CaretPosition = target;
                isAdjustingCaret = false;
            }
        }
    }
}