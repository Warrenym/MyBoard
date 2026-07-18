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
        private Controls.TextStylePicker? activeTextStylePicker;


        private void NoteDesignTool_AboutToOpen(object? sender, EventArgs e)
        {
            if (sender is not Controls.TextStylePicker picker) return;
            if (((MainViewModel)DataContext).CurrentBoard.PrimarySelectedItem is not NoteItemViewModel note) return;

            var richTextBox = FindNoteRichTextBox(note);
            picker.TargetRichTextBox = richTextBox;

            if (richTextBox != null)
            {
                Point topLeft = richTextBox.PointToScreen(new Point(0, 0));

                // Position the popover fully outside the note, to its left and
                // slightly above the top edge — not just nudged, but offset by
                // the popover's own approximate width so it never overlaps the note
                const double popoverWidth = 180;
                const double gap = 12;
                picker.AnchorScreenPoint = new Point(topLeft.X - popoverWidth - gap, topLeft.Y);
            }
        }

        private void NoteDesignTool_PopoverOpened(object? sender, EventArgs e)
        {
            IsSidebarExpanded = true;
            AnimateSidebarWidth(180);
        }


        private void NoteDesignTool_PopoverClosed(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("NoteDesignTool_PopoverClosed");
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

            var currentParagraph = rtb.CaretPosition.Paragraph;
            if (currentParagraph == null) return;

            // Clamp the caret inside quote marks if we're in a Quote Block —
            // runs first, before any key-specific handling below
            if (Services.NoteDocumentConverter.DetectBlockTypePublic(currentParagraph) == Model.NoteBlockType.QuoteBlock)
            {
                if (rtb.CaretPosition.CompareTo(currentParagraph.ContentStart) <= 0)
                    rtb.CaretPosition = currentParagraph.ContentStart.GetPositionAtOffset(1) ?? currentParagraph.ContentStart;
                else if (rtb.CaretPosition.CompareTo(currentParagraph.ContentEnd) >= 0)
                    rtb.CaretPosition = currentParagraph.ContentEnd.GetPositionAtOffset(-1) ?? currentParagraph.ContentEnd;
            }

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

            // Plain Enter: take full manual control — split the text at the caret,
            // create a new paragraph ourselves, and style it explicitly, rather
            // than racing WPF's own paragraph-creation timing
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;

                var caret = rtb.CaretPosition;
                var paragraph = caret.Paragraph;
                if (paragraph == null) return;

                var currentType = Services.NoteDocumentConverter.DetectBlockTypePublic(paragraph);

                // Quote Block is excluded from the reset — Enter inside a quote just
                // continues as another quote line, per your spec
                var targetType = currentType == Model.NoteBlockType.QuoteBlock
                    ? Model.NoteBlockType.QuoteBlock
                    : Model.NoteBlockType.Normal;

                var afterRange = new TextRange(caret, paragraph.ContentEnd);
                string afterText = afterRange.Text.TrimEnd('\r', '\n');
                afterRange.Text = "";

                var newParagraph = new Paragraph(new Run(afterText))
                {
                    Margin = Services.NoteDocumentConverter.ParagraphSpacing
                };
                rtb.Document.Blocks.InsertAfter(paragraph, newParagraph);

                rtb.CaretPosition = newParagraph.ContentStart;
                Services.NoteDocumentConverter.ApplyBlockType(rtb, targetType);
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