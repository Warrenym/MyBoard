using MyBoard.Model;
using MyBoard.Services;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MyBoard.Tests;

public sealed class NoteDocumentTests
{
    [Theory]
    [InlineData(NoteBlockType.Normal)]
    [InlineData(NoteBlockType.LargeHeading)]
    [InlineData(NoteBlockType.NormalHeading)]
    [InlineData(NoteBlockType.SmallText)]
    [InlineData(NoteBlockType.CodeBlock)]
    [InlineData(NoteBlockType.QuoteBlock)]
    public void Every_block_type_preserves_text_and_formatted_runs(NoteBlockType type) => Sta.Run(() =>
    {
        var source = Boards.Document("  世界 👋\t whitespace\nline break  ");
        source.Blocks[0].Type = type;

        var result = NoteDocumentConverter.ToNoteDocument(NoteDocumentConverter.ToFlowDocument(source));

        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(result));
    });

    [Fact]
    public void Empty_documents_and_paragraphs_normalize_to_editable_empty_blocks() => Sta.Run(() =>
    {
        foreach (var source in new[] { new NoteDocument(), NoteDocument.CreateEmpty() })
        {
            var result = NoteDocumentConverter.ToNoteDocument(NoteDocumentConverter.ToFlowDocument(source));
            var block = Assert.Single(result.Blocks);
            Assert.Equal(NoteBlockType.Normal, block.Type);
            Assert.Empty(block.Runs);
        }
    });

    [Fact]
    public void Missing_and_unknown_palette_keys_fall_back_to_default_colors() => Sta.Run(() =>
    {
        var source = new NoteDocument { Blocks = [new NoteBlock
        {
            Runs = [new NoteRun { Text = "missing" }, new NoteRun { Text = "unknown", TextColorKey = "alien", HighlightColorKey = "alien" }]
        }] };

        var flow = NoteDocumentConverter.ToFlowDocument(source);
        var result = NoteDocumentConverter.ToNoteDocument(flow);

        Assert.Equal("missingunknown", string.Concat(result.Blocks[0].Runs.Select(r => r.Text)));
        Assert.All(result.Blocks[0].Runs, r => { Assert.Null(r.TextColorKey); Assert.Null(r.HighlightColorKey); });
    });

    [Fact]
    public void Nested_spans_hyperlinks_and_wpf_line_breaks_are_preserved() => Sta.Run(() =>
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Bold(new Run("bold")));
        paragraph.Inlines.Add(new LineBreak());
        paragraph.Inlines.Add(new Hyperlink(new Italic(new Run("linked"))) { NavigateUri = new Uri("https://example.test/") });
        var flow = new FlowDocument(paragraph);

        var document = NoteDocumentConverter.ToNoteDocument(flow);

        Assert.Equal("bold\nlinked", string.Concat(document.Blocks[0].Runs.Select(r => r.Text)));
        Assert.True(document.Blocks[0].Runs[0].Bold);
        Assert.True(document.Blocks[0].Runs[2].Italic);
        Assert.Equal("https://example.test/", document.Blocks[0].Runs[2].LinkUrl);
        var again = NoteDocumentConverter.ToNoteDocument(NoteDocumentConverter.ToFlowDocument(document));
        Assert.Equal(JsonSerializer.Serialize(document), JsonSerializer.Serialize(again));
    });

    [Fact]
    public void Formatting_a_text_range_changes_only_that_range_and_not_another_note() => Sta.Run(() =>
    {
        var source = new NoteDocument { Blocks = [new NoteBlock { Runs = [new NoteRun { Text = "abcdef" }] }] };
        var first = NoteDocumentConverter.ToFlowDocument(source);
        var second = NoteDocumentConverter.ToFlowDocument(source);
        var run = Assert.IsType<Run>(Assert.IsType<Paragraph>(first.Blocks.FirstBlock).Inlines.FirstInline);
        var range = new TextRange(run.ContentStart.GetPositionAtOffset(1)!, run.ContentStart.GetPositionAtOffset(3)!);

        range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

        var changed = NoteDocumentConverter.ToNoteDocument(first);
        Assert.Equal("abcdef", string.Concat(changed.Blocks[0].Runs.Select(r => r.Text)));
        Assert.Equal("bc", string.Concat(changed.Blocks[0].Runs.Where(r => r.Bold).Select(r => r.Text)));
        Assert.Equal("adef", string.Concat(changed.Blocks[0].Runs.Where(r => !r.Bold).Select(r => r.Text)));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(NoteDocumentConverter.ToNoteDocument(second)));
        Assert.False(source.Blocks[0].Runs[0].Bold);
    });

    [Fact]
    public void Bullet_numbered_and_checkbox_lists_preserve_list_structure() => Sta.Run(() =>
    {
        var source = new NoteDocument { Blocks = Enum.GetValues<NoteListType>().Select(type => new NoteBlock
        {
            ListType = type, Runs = [new NoteRun { Text = type.ToString() }]
        }).ToList() };

        var result = NoteDocumentConverter.ToNoteDocument(NoteDocumentConverter.ToFlowDocument(source));

        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(result));
    });
}
