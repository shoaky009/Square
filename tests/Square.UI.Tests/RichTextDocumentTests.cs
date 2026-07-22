using System;
using Square.Extensions.RichText;
using Square.Text;
using Square.Extensions;
using Square.Controls;
using Square.Rendering;
using Square.UI;
using Xunit;

namespace Square.UI.Tests;

public class RichTextDocumentTests
{
    [Fact]
    public void FromPlainTextCreatesParagraphBlocks()
    {
        var document = RichTextDocument.FromPlainText("hello\nworld");

        Assert.Equal(2, document.Blocks.Count);
        Assert.All(document.Blocks, block => Assert.Equal(RichTextBlockKind.Paragraph, block.Kind));
        Assert.Equal("hello", document.Blocks[0].PlainText);
        Assert.Equal("world", document.Blocks[1].PlainText);
        Assert.Equal("hello\nworld", document.PlainText);
    }

    [Fact]
    public void NormalizeMergesAdjacentRunsWithSameMarks()
    {
        var marks = new RichTextMarks(Bold: true);
        var block = RichTextBlock.Paragraph(
            new RichTextRun("he", marks),
            new RichTextRun("", marks),
            new RichTextRun("llo", marks),
            new RichTextRun(" world", RichTextMarks.Empty));
        var document = new RichTextDocument([block]);

        document.Normalize();

        Assert.Equal(2, block.Inlines.Count);
        var first = Assert.IsType<RichTextRun>(block.Inlines[0]);
        var second = Assert.IsType<RichTextRun>(block.Inlines[1]);
        Assert.Equal("hello", first.Text);
        Assert.Equal(marks, first.Marks);
        Assert.Equal(" world", second.Text);
        Assert.Equal(RichTextMarks.Empty, second.Marks);
    }

    [Fact]
    public void SchemaRejectsInvalidHeadingLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTextBlock.Heading(7, new RichTextRun("too deep")));
    }

    [Fact]
    public void RunSlicePreservesMarks()
    {
        var marks = new RichTextMarks(Italic: true, Link: "https://example.test");
        var run = new RichTextRun("abcdef", marks);

        var slice = run.Slice(1, 3);

        Assert.Equal("bcd", slice.Text);
        Assert.Equal(marks, slice.Marks);
    }

    [Fact]
    public void EditorStateInsertsTextAtCaret()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("helo"));
        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 2)));

        state.InsertText("l");

        Assert.Equal("hello", state.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 3), state.Selection.Focus);
    }

    [Fact]
    public void EditorStateReplacesSelection()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello"));
        state.SetSelection(new RichTextSelection(new RichTextPosition(0, 1), new RichTextPosition(0, 4)));

        state.InsertText("i");

        Assert.Equal("hio", state.Document.PlainText);
        Assert.True(state.Selection.IsCollapsed);
        Assert.Equal(new RichTextPosition(0, 2), state.Selection.Focus);
    }

    [Fact]
    public void EditorStateInsertParagraphSplitsBlock()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello"));
        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 2)));

        state.InsertParagraph();

        Assert.Equal(2, state.Document.Blocks.Count);
        Assert.Equal("he", state.Document.Blocks[0].PlainText);
        Assert.Equal("llo", state.Document.Blocks[1].PlainText);
        Assert.Equal(new RichTextPosition(1, 0), state.Selection.Focus);
    }

    [Fact]
    public void EditorStateBackspaceAtBlockStartJoinsBlocks()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello\nworld"));
        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(1, 0)));

        Assert.True(state.DeleteBackward());

        Assert.Single(state.Document.Blocks);
        Assert.Equal("helloworld", state.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 5), state.Selection.Focus);
    }

    [Fact]
    public void EditorStateDeleteForwardDeletesCharacterAndJoinsBlocks()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello\nworld"));
        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 1)));

        Assert.True(state.DeleteForward());
        Assert.Equal("hllo\nworld", state.Document.PlainText);

        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 4)));
        Assert.True(state.DeleteForward());
        Assert.Equal("hlloworld", state.Document.PlainText);
        Assert.Single(state.Document.Blocks);
    }

    [Fact]
    public void EditorStateUndoRedoRestoresSnapshots()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello"));
        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 5)));
        state.InsertText("!");

        Assert.Equal("hello!", state.Document.PlainText);
        Assert.True(state.Undo());
        Assert.Equal("hello", state.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 5), state.Selection.Focus);
        Assert.True(state.Redo());
        Assert.Equal("hello!", state.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 6), state.Selection.Focus);
    }

    [Fact]
    public void EditorStateDeletesSelection()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello"));
        state.SetSelection(new RichTextSelection(new RichTextPosition(0, 1), new RichTextPosition(0, 4)));

        Assert.True(state.DeleteSelection());

        Assert.Equal("ho", state.Document.PlainText);
        Assert.True(state.Selection.IsCollapsed);
        Assert.Equal(new RichTextPosition(0, 1), state.Selection.Focus);
    }

    [Fact]
    public void RichTextEditorHandlesTextInput()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hi"));
        editor.HandleTextInput("!");

        Assert.Equal("!hi", editor.PlainText);
        Assert.Equal(1, editor.CaretIndex);
    }

    [Fact]
    public void RichTextEditorMeasuresToFiniteAvailableWidth()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("short"));

        var size = editor.Measure(new Square.Graphics.Size(480, 600));

        Assert.Equal(480, size.Width);
    }

    [Fact]
    public void RichTextEditorStretchesInColumnFlexPane()
    {
        var pane = new View();
        pane.Style.Set("display", "flex");
        pane.Style.Set("flex-direction", "column");
        pane.Style.Set("padding", "12px");
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("short"));
        editor.Style.Set("width", "100%");
        editor.Style.Set("min-height", "120px");
        editor.Style.Set("flex-grow", "1");
        editor.Style.Set("flex-shrink", "1");
        pane.Children.Add(editor);

        var layout = new LayoutEngine();
        layout.Measure(pane, new Square.Graphics.Size(680, 220));
        layout.Arrange(pane, new Square.Graphics.Rect(0, 0, 680, 220));

        Assert.Equal(656, editor.Geometry.Width);
    }

    [Fact]
    public void RichTextEditorRegistersAsExtensionElement()
    {
        ExtensionRegistration.RegisterDefaults();
        var document = new UIDocument();

        var element = document.CreateElement("RichTextEditor");

        Assert.IsType<RichTextEditor>(element);
        Assert.Same(document, element.OwnerDocument);
    }

    [Fact]
    public void EditorStateAppliesBoldMarkToSelection()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("hello"));
        state.SetSelection(new RichTextSelection(new RichTextPosition(0, 1), new RichTextPosition(0, 4)));

        state.ToggleMarks(new RichTextMarks(Bold: true));

        var block = state.Document.Blocks[0];
        Assert.Equal(3, block.Inlines.Count);
        Assert.False(Assert.IsType<RichTextRun>(block.Inlines[0]).Marks.Bold);
        Assert.True(Assert.IsType<RichTextRun>(block.Inlines[1]).Marks.Bold);
        Assert.False(Assert.IsType<RichTextRun>(block.Inlines[2]).Marks.Bold);
        Assert.Equal("ell", Assert.IsType<RichTextRun>(block.Inlines[1]).Text);
    }

    [Fact]
    public void EditorStateTogglesBoldOffWithoutClearingItalic()
    {
        var marks = new RichTextMarks(Bold: true, Italic: true);
        var state = new RichTextEditorState(new RichTextDocument([
            RichTextBlock.Paragraph(new RichTextRun("hello", marks))
        ]));
        state.SetSelection(new RichTextSelection(new RichTextPosition(0, 0), new RichTextPosition(0, 5)));

        state.ToggleMarks(new RichTextMarks(Bold: true));

        var run = Assert.IsType<RichTextRun>(Assert.Single(state.Document.Blocks[0].Inlines));
        Assert.False(run.Marks.Bold);
        Assert.True(run.Marks.Italic);
    }

    [Fact]
    public void RichTextEditorCtrlBTogglesBoldOnSelection()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"))
        {
            Geometry = new Square.Graphics.Rect(0, 0, 200, 80)
        };
        editor.HandlePointerDown(new Square.Graphics.Point(15, 8));
        editor.HandlePointerMove(new Square.Graphics.Point(36, 8));
        editor.HandlePointerUp(new Square.Graphics.Point(36, 8));

        editor.HandleKey(66, control: true);

        Assert.Contains(editor.Document.Blocks[0].Inlines, inline =>
            inline is RichTextRun { Marks.Bold: true });
    }

    [Fact]
    public void RichTextLayoutPreservesOffsetsAcrossRuns()
    {
        var block = RichTextBlock.Paragraph(
            new RichTextRun("ab"),
            new RichTextRun("中", new RichTextMarks(Bold: true)),
            new RichTextRun("cd"));
        var layout = RichTextLayoutEngine.LayoutBlock(
            block,
            new Square.Graphics.Font("sans-serif", 20),
            new Square.Graphics.Point(10, 20),
            200,
            24);

        Assert.Single(layout.Lines);
        Assert.Equal(3, layout.Lines[0].Fragments.Count);
        Assert.Equal(0, layout.Lines[0].Fragments[0].StartOffset);
        Assert.Equal(2, layout.Lines[0].Fragments[0].EndOffset);
        Assert.Equal(2, layout.Lines[0].Fragments[1].StartOffset);
        Assert.Equal(3, layout.Lines[0].Fragments[1].EndOffset);
        Assert.Equal(3, layout.Lines[0].Fragments[2].StartOffset);
        Assert.Equal(5, layout.Lines[0].Fragments[2].EndOffset);
        Assert.Equal(layout.Lines[0].Fragments[1].Bounds.Right, layout.GetCaretRect(3).X);
    }

    [Fact]
    public void RichTextLayoutWrapsAndProducesSelectionRects()
    {
        var block = RichTextBlock.Paragraph(new RichTextRun("abcdef"));
        var layout = RichTextLayoutEngine.LayoutBlock(
            block,
            new Square.Graphics.Font("sans-serif", 20),
            new Square.Graphics.Point(0, 0),
            30,
            24);

        Assert.Equal(2, layout.Lines.Count);
        Assert.Equal(0, layout.Lines[0].StartOffset);
        Assert.Equal(3, layout.Lines[0].EndOffset);
        Assert.Equal(3, layout.Lines[1].StartOffset);
        Assert.Equal(6, layout.Lines[1].EndOffset);
        var selectionRects = layout.GetSelectionRects(1, 5);
        Assert.Equal(2, selectionRects.Count);
        Assert.Equal(20, selectionRects[0].Width);
        Assert.Equal(20, selectionRects[1].Width);
        Assert.Equal(1, layout.HitTestOffset(new Square.Graphics.Point(12, 5)));
        Assert.Equal(4, layout.HitTestOffset(new Square.Graphics.Point(12, 29)));
    }

    [Fact]
    public void RichTextLayoutSelectionRectsUseBoldAdvance()
    {
        var block = RichTextBlock.Paragraph(
            new RichTextRun("aa", new RichTextMarks(Bold: true)),
            new RichTextRun("aa"));
        var layout = RichTextLayoutEngine.LayoutBlock(
            block,
            new Square.Graphics.Font("sans-serif", 20),
            new Square.Graphics.Point(0, 0),
            200,
            24);

        var boldWidth = layout.GetSelectionRects(0, 2).Single().Width;
        var normalWidth = layout.GetSelectionRects(2, 4).Single().Width;

        Assert.Equal(layout.Lines[0].Fragments[0].Bounds.Width, boldWidth);
        Assert.Equal(layout.Lines[0].Fragments[0].Bounds.Right, layout.GetCaretRect(2).X);
        Assert.True(boldWidth > normalWidth);
    }

    [Fact]
    public void RichTextWordBoundarySelectsWordWithoutPunctuation()
    {
        Assert.Equal((0, 10), RichTextBoundaries.WordAt("formatting, text", 4));
        Assert.Equal((10, 11), RichTextBoundaries.WordAt("formatting, text", 10));
        Assert.Equal((12, 16), RichTextBoundaries.WordAt("formatting, text", 14));
    }

    [Fact]
    public void RichTextEditorSelectWordAtSelectsClickedWord()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("bold plain"))
        {
            Geometry = new Square.Graphics.Rect(0, 0, 240, 80)
        };

        editor.SelectWordAt(new Square.Graphics.Point(20, 10));

        Assert.Equal("bold", editor.SelectedText);
    }

    [Fact]
    public void RichTextEditorMovesAcrossVisualLinesAndLineBoundaries()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("abcdef"))
        {
            Geometry = new Square.Graphics.Rect(0, 0, 46, 100)
        };
        editor.HandlePointerDown(new Square.Graphics.Point(15, 10));
        editor.HandlePointerUp(new Square.Graphics.Point(15, 10));

        editor.HandleKey(40);
        Assert.Equal(5, editor.CaretIndex);

        editor.HandleKey(36);
        Assert.Equal(4, editor.CaretIndex);

        editor.HandleKey(35);
        Assert.Equal(6, editor.CaretIndex);

        editor.HandleKey(38);
        Assert.Equal(2, editor.CaretIndex);
    }

    [Fact]
    public void RichTextEditorPublicToolbarCommandsWork()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"));
        editor.SelectAll();
        editor.SetForeground("#175cd3");
        editor.ToggleBold();

        var formatted = Assert.IsType<RichTextRun>(Assert.Single(editor.Document.Blocks[0].Inlines));
        Assert.Equal("#175cd3", formatted.Marks.Foreground);
        Assert.True(formatted.Marks.Bold);
        Assert.True(editor.CanUndo);

        editor.ClearFormatting();
        var cleared = Assert.IsType<RichTextRun>(Assert.Single(editor.Document.Blocks[0].Inlines));
        Assert.True(cleared.Marks.IsEmpty);
        Assert.True(editor.Undo());
        Assert.True(Assert.IsType<RichTextRun>(Assert.Single(editor.Document.Blocks[0].Inlines)).Marks.Bold);
        Assert.True(editor.Redo());
        Assert.True(Assert.IsType<RichTextRun>(Assert.Single(editor.Document.Blocks[0].Inlines)).Marks.IsEmpty);
    }

    [Fact]
    public void RichTextEditorFormattingCommandsDispatchInput()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"));
        var inputEvents = 0;
        editor.AddEventListener("input", _ => inputEvents++);
        editor.SelectAll();

        editor.ToggleBold();
        editor.ToggleItalic();
        editor.ToggleUnderline();

        Assert.Equal(3, inputEvents);
    }

    [Fact]
    public void RichTextEditorCanCollapseSelectionToEnd()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"));
        editor.SelectAll();

        editor.CollapseSelectionToEnd();

        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal(5, editor.CaretIndex);
    }

    [Fact]
    public void RichTextEditorResolvesGenericCssFontLikeNormalText()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"));
        editor.Style.Set("font-family", "sans-serif");
        editor.Style.Set("font-size", "16px");
        editor.Style.Set("font-weight", "700");
        editor.Style.Set("font-style", "italic");
        var resolveFont = typeof(RichTextEditor).GetMethod(
            "ResolveFont",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var font = Assert.IsType<Square.Graphics.Font>(resolveFont.Invoke(editor, null));
        var expected = FontManager.Instance.FromCss("sans-serif", "16px", "700", "italic", 14);

        Assert.Equal(expected.Family, font.Family);
        Assert.Equal(expected.Size, font.Size);
        Assert.Equal(expected.Weight, font.Weight);
        Assert.Equal(expected.Style, font.Style);
    }

    [Fact]
    public void RichTextEditorMeasureUsesCssPadding()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"));
        editor.Style.Set("font-size", "16px");
        editor.Style.Set("padding", "18px");

        var measured = editor.Measure(new Square.Graphics.Size(float.PositiveInfinity, float.PositiveInfinity));
        var font = FontManager.Instance.FromCss(null, "16px", null, null, 14);
        var expectedTextWidth = RichTextLayoutEngine.LayoutBlock(
            editor.Document.Blocks[0],
            font,
            Square.Graphics.Point.Zero,
            float.PositiveInfinity,
            font.Size * Square.Graphics.TextLayout.DefaultLineHeight).Bounds.Width;

        Assert.Equal(expectedTextWidth + 36, measured.Width);
    }

    [Fact]
    public void UnicodeBoundariesKeepEmojiAndCombiningMarksIntact()
    {
        const string text = "A😀e\u0301中";

        Assert.Equal(1, RichTextBoundaries.NextTextElement(text, 0));
        Assert.Equal(3, RichTextBoundaries.NextTextElement(text, 1));
        Assert.Equal(5, RichTextBoundaries.NextTextElement(text, 3));
        Assert.Equal(3, RichTextBoundaries.PreviousTextElement(text, 5));
        Assert.Equal(1, RichTextBoundaries.PreviousTextElement(text, 3));
    }

    [Fact]
    public void EditorStateDeletesWholeUnicodeTextElements()
    {
        var state = new RichTextEditorState(RichTextDocument.FromPlainText("A😀e\u0301B"));
        state.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 3)));

        Assert.True(state.DeleteBackward());
        Assert.Equal("Ae\u0301B", state.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 1), state.Selection.Focus);

        Assert.True(state.DeleteForward());
        Assert.Equal("AB", state.Document.PlainText);
    }

    [Fact]
    public void RichTextEditorMovesByWordsWithControlArrow()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("one  two 中 文"));

        editor.HandleKey(39, control: true);
        Assert.Equal(5, editor.CaretIndex);
        editor.HandleKey(39, control: true);
        Assert.Equal(9, editor.CaretIndex);
        editor.HandleKey(37, control: true);
        Assert.Equal(5, editor.CaretIndex);
    }

    [Fact]
    public void RichTextFragmentCodecRoundTripsBlocksAndMarks()
    {
        var fragment = new RichTextFragment([
            RichTextBlock.Heading(2, new RichTextRun("Title", new RichTextMarks(Bold: true, Foreground: "#175cd3"))),
            RichTextBlock.Paragraph(new RichTextRun("Body", new RichTextMarks(Italic: true, Link: "https://example.test")))
        ]);

        var json = RichTextFragmentCodec.Serialize(fragment);
        var roundTrip = RichTextFragmentCodec.Deserialize(json);

        Assert.Equal("Title\nBody", roundTrip.PlainText);
        Assert.Equal(RichTextBlockKind.Heading, roundTrip.Blocks[0].Kind);
        Assert.Equal(2, roundTrip.Blocks[0].HeadingLevel);
        var title = Assert.IsType<RichTextRun>(Assert.Single(roundTrip.Blocks[0].Inlines));
        Assert.True(title.Marks.Bold);
        Assert.Equal("#175cd3", title.Marks.Foreground);
        var body = Assert.IsType<RichTextRun>(Assert.Single(roundTrip.Blocks[1].Inlines));
        Assert.True(body.Marks.Italic);
        Assert.Equal("https://example.test", body.Marks.Link);
    }

    [Fact]
    public void EditorStateCopiesAndPastesRichFragmentAcrossBlocks()
    {
        var source = new RichTextEditorState(new RichTextDocument([
            RichTextBlock.Paragraph(new RichTextRun("hello", new RichTextMarks(Bold: true))),
            RichTextBlock.Quote(new RichTextRun("world", new RichTextMarks(Italic: true)))
        ]));
        source.SetSelection(new RichTextSelection(new RichTextPosition(0, 2), new RichTextPosition(1, 3)));
        var fragment = source.GetSelectedFragment();

        var target = new RichTextEditorState(RichTextDocument.FromPlainText("AB"));
        target.SetSelection(RichTextSelection.Collapsed(new RichTextPosition(0, 1)));
        target.InsertFragment(fragment);

        Assert.Equal("Allo\nworB", target.Document.PlainText);
        Assert.Equal(2, target.Document.Blocks.Count);
        Assert.Contains(target.Document.Blocks[0].Inlines, inline => inline is RichTextRun { Marks.Bold: true });
        Assert.Contains(target.Document.Blocks[1].Inlines, inline => inline is RichTextRun { Marks.Italic: true });
        Assert.Equal(new RichTextPosition(1, 3), target.Selection.Focus);
    }

    [Fact]
    public void RichTextEditorJsonPasteRejectsInvalidPayload()
    {
        var editor = new RichTextEditor(RichTextDocument.FromPlainText("hello"));

        Assert.False(editor.InsertRichText("not json"));
        Assert.Equal("hello", editor.PlainText);
    }
}
