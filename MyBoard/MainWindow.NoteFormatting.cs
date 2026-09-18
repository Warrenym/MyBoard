using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace MyBoard
{
    public partial class MainWindow
    {
        // The RichTextBox the formatting toolbar currently acts on — set
        // whenever a note's design panel becomes active, mirroring how
        // TextStylePicker tracks its own target
        private RichTextBox? activeFormattingTarget;

        // Strikethrough has no built-in EditingCommand (unlike Bold/Italic/
        // Underline), so when the toggle is clicked with an empty selection
        // (just a caret) we remember the intended state here and apply it
        // to text as it's typed — see NoteRichTextBox_TextChanged.
        private bool? pendingStrikethroughState;
        private RichTextBox? pendingStrikethroughTarget;
        private bool strikethroughTypingInProgress;

        // Direct references to the format toggle buttons, captured via their
        // own Loaded event. These buttons live inside a DataTemplate, which
        // gets its own private runtime NameScope — FindName() from the
        // window's root Content cannot see into it and silently returns
        // null, so looking them up that way never actually worked.
        private ToggleButton? boldToggleRef;
        private ToggleButton? italicToggleRef;
        private ToggleButton? underlineToggleRef;
        private ToggleButton? strikeToggleRef;
        private ToggleButton? bulletToggleRef;
        private ToggleButton? numberedToggleRef;
        private ToggleButton? checkboxToggleRef;

        private void FormatToggle_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggle || toggle.Tag is not string format) return;

            switch (format)
            {
                case "Bold": boldToggleRef = toggle; break;
                case "Italic": italicToggleRef = toggle; break;
                case "Underline": underlineToggleRef = toggle; break;
                case "Strikethrough": strikeToggleRef = toggle; break;
                case "Bullet": bulletToggleRef = toggle; break;
                case "Numbered": numberedToggleRef = toggle; break;
                case "Checkbox": checkboxToggleRef = toggle; break;
            }
        }

        // Called from NoteRichTextBox's own SelectionChanged (the note
        // itself, not the toolbar) — keeps toggle buttons in sync live as
        // the caret moves, not just after a click
        private void NoteRichTextBox_RefreshFormatState(RichTextBox rtb)
        {
            activeFormattingTarget = rtb;

            // A pending strikethrough state survives the SelectionChanged that
            // immediately follows a keystroke (the caret advancing by one
            // character) so it keeps applying to the rest of what's typed.
            // Any other selection change (click, arrow keys, real selection)
            // means the caret moved for a reason other than our own typing,
            // so the pending state is stale and should be dropped.
            if (pendingStrikethroughTarget == rtb)
            {
                if (strikethroughTypingInProgress)
                    strikethroughTypingInProgress = false;
                else
                {
                    pendingStrikethroughTarget = null;
                    pendingStrikethroughState = null;
                }
            }

            var selection = rtb.Selection;
            SetToggleState("BoldToggle", selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw && fw == FontWeights.Bold);
            SetToggleState("ItalicToggle", selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fs && fs == FontStyles.Italic);
            SetToggleState("UnderlineToggle", HasDecoration(selection, TextDecorations.Underline));

            bool strikeActive = pendingStrikethroughTarget == rtb
                ? pendingStrikethroughState!.Value
                : HasDecoration(selection, TextDecorations.Strikethrough);
            SetToggleState("StrikeToggle", strikeActive);

            var listType = GetCurrentListType(rtb);
            SetToggleState("BulletToggle", listType == Model.NoteListType.Bullet);
            SetToggleState("NumberedToggle", listType == Model.NoteListType.Numbered);
            SetToggleState("CheckboxToggle", listType == Model.NoteListType.Checkbox);
        }

        private static bool HasDecoration(TextSelection selection, TextDecorationCollection target)
        {
            var value = selection.GetPropertyValue(Inline.TextDecorationsProperty);
            return value is TextDecorationCollection decorations &&
                   decorations.Count > 0 &&
                   decorations[0].Location == target[0].Location;
        }

        // Sets a toggle button's checked state using the direct reference
        // captured in FormatToggle_Loaded, instead of FindName (which
        // cannot see into a DataTemplate's private NameScope)
        private void SetToggleState(string name, bool isActive)
        {
            var toggle = name switch
            {
                "BoldToggle" => boldToggleRef,
                "ItalicToggle" => italicToggleRef,
                "UnderlineToggle" => underlineToggleRef,
                "StrikeToggle" => strikeToggleRef,
                "BulletToggle" => bulletToggleRef,
                "NumberedToggle" => numberedToggleRef,
                "CheckboxToggle" => checkboxToggleRef,
                _ => null
            };

            if (toggle != null)
                toggle.IsChecked = isActive;
        }

        // Applies the clicked format to the current selection/caret using
        // WPF's built-in EditingCommands — these already handle "toggle on
        // if off, toggle off if on" correctly on their own, AND correctly
        // handle an empty (caret-only) selection by setting the format that
        // will be used for the next typed characters. Manually calling
        // TextSelection.ApplyPropertyValue (as this used to do) is a no-op
        // on an empty selection, which is why typing didn't pick up the
        // format and toggles appeared to "snap back".
        private void FormatToggle_Click(object sender, RoutedEventArgs e)
        {
            if (activeFormattingTarget == null) return;
            if (sender is not ToggleButton toggle || toggle.Tag is not string format) return;

            switch (format)
            {
                case "Bold":
                    EditingCommands.ToggleBold.Execute(null, activeFormattingTarget);
                    break;

                case "Italic":
                    EditingCommands.ToggleItalic.Execute(null, activeFormattingTarget);
                    break;

                case "Underline":
                    EditingCommands.ToggleUnderline.Execute(null, activeFormattingTarget);
                    break;

                case "Strikethrough":
                    ToggleStrikethrough(activeFormattingTarget);
                    break;

                case "Bullet":
                    ApplyListFormat(activeFormattingTarget, Model.NoteListType.Bullet);
                    break;

                case "Numbered":
                    ApplyListFormat(activeFormattingTarget, Model.NoteListType.Numbered);
                    break;

                case "Checkbox":
                    ApplyListFormat(activeFormattingTarget, Model.NoteListType.Checkbox);
                    break;
            }

            NoteRichTextBox_RefreshFormatState(activeFormattingTarget);
            activeFormattingTarget.Focus();
        }

        private static System.Windows.Documents.List? GetCurrentList(RichTextBox rtb) =>
            rtb.CaretPosition.Paragraph?.Parent is ListItem item
                ? item.Parent as System.Windows.Documents.List
                : null;

        private static Model.NoteListType? GetCurrentListType(RichTextBox rtb)
        {
            var list = GetCurrentList(rtb);
            if (list == null) return null;
            if (list.MarkerStyle == TextMarkerStyle.Decimal) return Model.NoteListType.Numbered;

            var paragraph = rtb.CaretPosition.Paragraph;
            if (paragraph?.Inlines.FirstInline is Run { Tag: Services.NoteDocumentConverter.ChecklistGlyphTag })
                return Model.NoteListType.Checkbox;

            return Model.NoteListType.Bullet;
        }

        private void ApplyListFormat(RichTextBox rtb, Model.NoteListType requestedType)
        {
            var currentType = GetCurrentListType(rtb);
            var currentList = GetCurrentList(rtb);

            if (currentType == requestedType && currentList != null)
            {
                RemoveCheckboxes(currentList);
                currentList.MarkerStyle = requestedType == Model.NoteListType.Numbered
                    ? TextMarkerStyle.Decimal
                    : TextMarkerStyle.Disc;

                var command = requestedType == Model.NoteListType.Numbered
                    ? EditingCommands.ToggleNumbering
                    : EditingCommands.ToggleBullets;
                command.Execute(null, rtb);
                return;
            }

            if (currentList == null)
            {
                var command = requestedType == Model.NoteListType.Numbered
                    ? EditingCommands.ToggleNumbering
                    : EditingCommands.ToggleBullets;
                command.Execute(null, rtb);
                currentList = GetCurrentList(rtb);
            }

            if (currentList == null) return;

            RemoveCheckboxes(currentList);
            currentList.MarkerStyle = requestedType switch
            {
                Model.NoteListType.Numbered => TextMarkerStyle.Decimal,
                Model.NoteListType.Checkbox => TextMarkerStyle.None,
                _ => TextMarkerStyle.Disc
            };

            if (requestedType == Model.NoteListType.Checkbox)
                AddCheckboxes(currentList);
        }

        private void AddCheckboxes(System.Windows.Documents.List list)
        {
            // WPF invalidates its live TextElement enumerator when any nested
            // inline is changed, so snapshot the items before editing them.
            var items = list.ListItems.Cast<ListItem>().ToArray();
            foreach (var item in items)
            {
                if (item.Blocks.FirstBlock is not Paragraph paragraph) continue;
                if (paragraph.Inlines.FirstInline is Run { Tag: Services.NoteDocumentConverter.ChecklistGlyphTag }) continue;

                var checkbox = CreateChecklistGlyphRun();

                if (paragraph.Inlines.FirstInline is Inline firstInline)
                    paragraph.Inlines.InsertBefore(firstInline, checkbox);
                else
                    paragraph.Inlines.Add(checkbox);
            }
        }

        private static Run CreateChecklistGlyphRun(bool isChecked = false) => new(isChecked ? "☑ " : "☐ ")
        {
            Tag = Services.NoteDocumentConverter.ChecklistGlyphTag,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
            FontSize = 16,
            Cursor = Cursors.Hand,
            TextDecorations = new TextDecorationCollection()
        };

        private static void RemoveCheckboxes(System.Windows.Documents.List list)
        {
            var items = list.ListItems.Cast<ListItem>().ToArray();
            foreach (var item in items)
            {
                if (item.Blocks.FirstBlock is not Paragraph paragraph) continue;
                if (paragraph.Inlines.FirstInline is Run { Tag: Services.NoteDocumentConverter.ChecklistGlyphTag } checkbox)
                    paragraph.Inlines.Remove(checkbox);
            }
        }

        private static void SetChecklistItemStrikethrough(Paragraph paragraph, bool isChecked)
        {
            foreach (var run in paragraph.Inlines.OfType<Run>())
            {
                if (run.Tag as string == Services.NoteDocumentConverter.ChecklistGlyphTag)
                    continue;

                // Keep an explicit empty collection when unchecked. A null
                // value would inherit strikethrough from the prior typing run.
                run.TextDecorations = WithoutStrikethrough(run.TextDecorations, isChecked);
            }
        }

        private static TextDecorationCollection WithoutStrikethrough(
            TextDecorationCollection? existing,
            bool isChecked)
        {
            var updated = new TextDecorationCollection();
            if (existing != null)
            {
                foreach (var decoration in existing)
                {
                    if (decoration.Location != TextDecorations.Strikethrough[0].Location)
                        updated.Add(decoration.Clone());
                }
            }

            if (isChecked)
                updated.Add(TextDecorations.Strikethrough[0].Clone());

            return updated;
        }

        // Strikethrough has no built-in EditingCommand, so we toggle it
        // manually. For a real selection that's a direct property apply.
        // For a caret-only position there's nothing to apply the property
        // to yet, so remember the intended state and apply it to text as
        // it's typed (see NoteRichTextBox_TextChanged).
        private void ToggleStrikethrough(RichTextBox rtb)
        {
            if (!rtb.Selection.IsEmpty)
            {
                bool alreadyStruck = HasDecoration(rtb.Selection, TextDecorations.Strikethrough);
                rtb.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
                    alreadyStruck ? null : TextDecorations.Strikethrough);

                pendingStrikethroughTarget = null;
                pendingStrikethroughState = null;
                return;
            }

            bool currentlyStruck = HasDecoration(rtb.Selection, TextDecorations.Strikethrough);
            pendingStrikethroughState = !currentlyStruck;
            pendingStrikethroughTarget = rtb;
        }

        // Fires as text is typed. If a strikethrough toggle is pending for
        // this RichTextBox (set from a caret-only click above), apply it to
        // the character(s) that were just inserted.
        private void NoteRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not RichTextBox rtb) return;

            if (pendingStrikethroughTarget != rtb || pendingStrikethroughState is not bool wantStrike) return;
            if (!strikethroughTypingInProgress) return; // only apply for actual typed input

            var caret = rtb.CaretPosition;
            var start = caret.GetPositionAtOffset(-1) ?? caret;
            new TextRange(start, caret).ApplyPropertyValue(Inline.TextDecorationsProperty,
                wantStrike ? TextDecorations.Strikethrough : null);
        }
    }
}
