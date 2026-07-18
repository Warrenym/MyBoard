using MyBoard.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;

namespace MyBoard
{
    public partial class MainWindow
    {
        // The RichTextBox the formatting toolbar currently acts on — set
        // whenever a note's design panel becomes active, mirroring how
        // TextStylePicker tracks its own target
        private RichTextBox? activeFormattingTarget;

        // Direct references to the format toggle buttons, captured via their
        // own Loaded event. These buttons live inside a DataTemplate, which
        // gets its own private runtime NameScope — FindName() from the
        // window's root Content cannot see into it and silently returns
        // null, so looking them up that way never actually worked.
        private ToggleButton? boldToggleRef;
        private ToggleButton? italicToggleRef;
        private ToggleButton? underlineToggleRef;
        private ToggleButton? strikeToggleRef;

        private void FormatToggle_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggle || toggle.Tag is not string format) return;

            switch (format)
            {
                case "Bold": boldToggleRef = toggle; break;
                case "Italic": italicToggleRef = toggle; break;
                case "Underline": underlineToggleRef = toggle; break;
                case "Strikethrough": strikeToggleRef = toggle; break;
            }
        }

        // Called from NoteRichTextBox's own SelectionChanged (the note
        // itself, not the toolbar) — keeps toggle buttons in sync live as
        // the caret moves, not just after a click
        private void NoteRichTextBox_RefreshFormatState(RichTextBox rtb)
        {
            activeFormattingTarget = rtb;

            var selection = rtb.Selection;
            SetToggleState("BoldToggle", selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw && fw == FontWeights.Bold);
            SetToggleState("ItalicToggle", selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fs && fs == FontStyles.Italic);
            SetToggleState("UnderlineToggle", HasDecoration(selection, TextDecorations.Underline));
            SetToggleState("StrikeToggle", HasDecoration(selection, TextDecorations.Strikethrough));
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
                _ => null
            };

            if (toggle != null)
                toggle.IsChecked = isActive;
        }

        // Applies the clicked format to the current selection/caret using
        // WPF's built-in EditingCommands — these already handle "toggle on
        // if off, toggle off if on" correctly on their own
        private void FormatToggle_Click(object sender, RoutedEventArgs e)
        {
            if (activeFormattingTarget == null) return;
            if (sender is not ToggleButton toggle || toggle.Tag is not string format) return;

            var selection = activeFormattingTarget.Selection;
            if (selection.IsEmpty) return; // Nothing selected — nothing to format yet

            switch (format)
            {
                case "Bold":
                    bool isBold = selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw && fw == FontWeights.Bold;
                    selection.ApplyPropertyValue(TextElement.FontWeightProperty, isBold ? FontWeights.Normal : FontWeights.Bold);
                    break;

                case "Italic":
                    bool isItalic = selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fst && fst == FontStyles.Italic;
                    selection.ApplyPropertyValue(TextElement.FontStyleProperty, isItalic ? FontStyles.Normal : FontStyles.Italic);
                    break;

                case "Underline":
                    bool hasUnderline = HasDecoration(selection, TextDecorations.Underline);
                    selection.ApplyPropertyValue(Inline.TextDecorationsProperty, hasUnderline ? null : TextDecorations.Underline);
                    break;

                case "Strikethrough":
                    ToggleStrikethrough(activeFormattingTarget);
                    break;
            }

            activeFormattingTarget.Focus();
        }

        // Strikethrough has no built-in EditingCommand, so we toggle it
        // manually on the current selection
        private void ToggleStrikethrough(RichTextBox rtb)
        {
            bool alreadyStruck = HasDecoration(rtb.Selection, TextDecorations.Strikethrough);
            rtb.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
                alreadyStruck ? null : TextDecorations.Strikethrough);
        }
    }
}