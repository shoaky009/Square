namespace Square.Extensions.RichText;

public sealed class RichTextEditorState
{
    private readonly Stack<Snapshot> _undo = [];
    private readonly Stack<Snapshot> _redo = [];

    public RichTextEditorState(RichTextDocument? document = null)
    {
        Document = document ?? new RichTextDocument();
        Document.Normalize();
        Selection = RichTextSelection.Collapsed(new RichTextPosition(0, 0));
        ActiveMarks = ResolveMarksAt(Selection.Start);
    }

    public RichTextDocument Document { get; }
    public RichTextSelection Selection { get; private set; }
    public RichTextMarks ActiveMarks { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void SetSelection(RichTextSelection selection)
    {
        ValidatePosition(selection.Anchor);
        ValidatePosition(selection.Focus);
        Selection = selection;
        if (selection.IsCollapsed)
            ActiveMarks = ResolveMarksAt(selection.Start);
    }

    public void InsertText(string text, RichTextMarks? marks = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        Execute(() =>
        {
            DeleteSelectionCore();
            var position = Selection.Start;
            var parts = NormalizeNewlines(text).Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length > 0)
                {
                    InsertTextCore(position, part, marks ?? ActiveMarks);
                    position = new RichTextPosition(position.BlockIndex, position.Offset + part.Length);
                }

                if (i < parts.Length - 1)
                    position = SplitBlockCore(position);
            }
            Selection = RichTextSelection.Collapsed(position);
            ActiveMarks = marks ?? ActiveMarks;
        });
    }

    public bool DeleteBackward()
    {
        if (!Selection.IsCollapsed)
        {
            DeleteSelection();
            return true;
        }

        var position = Selection.Start;
        if (position.BlockIndex == 0 && position.Offset == 0) return false;

        Execute(() =>
        {
            if (position.Offset > 0)
            {
                var block = Document.Blocks[position.BlockIndex];
                var previousOffset = RichTextBoundaries.PreviousTextElement(block.PlainText, position.Offset);
                DeleteRangeCore(new RichTextSelection(
                    new RichTextPosition(position.BlockIndex, previousOffset),
                    position));
                Selection = RichTextSelection.Collapsed(new RichTextPosition(position.BlockIndex, previousOffset));
                return;
            }

            var previous = Document.Blocks[position.BlockIndex - 1];
            var current = Document.Blocks[position.BlockIndex];
            var offset = previous.PlainText.Length;
            previous.Inlines.AddRange(CloneInlines(current.Inlines));
            Document.Blocks.RemoveAt(position.BlockIndex);
            previous.Normalize();
            Selection = RichTextSelection.Collapsed(new RichTextPosition(position.BlockIndex - 1, offset));
        });
        return true;
    }

    public bool DeleteSelection()
    {
        if (Selection.IsCollapsed) return false;
        Execute(DeleteSelectionCore);
        return true;
    }

    public bool DeleteForward()
    {
        if (!Selection.IsCollapsed) return DeleteSelection();

        var position = Selection.Start;
        var block = Document.Blocks[position.BlockIndex];
        if (position.BlockIndex == Document.Blocks.Count - 1 && position.Offset == block.PlainText.Length)
            return false;

        Execute(() =>
        {
            if (position.Offset < block.PlainText.Length)
            {
                var nextOffset = RichTextBoundaries.NextTextElement(block.PlainText, position.Offset);
                DeleteRangeCore(new RichTextSelection(
                    position,
                    new RichTextPosition(position.BlockIndex, nextOffset)));
                Selection = RichTextSelection.Collapsed(position);
                return;
            }

            var next = Document.Blocks[position.BlockIndex + 1];
            block.Inlines.AddRange(CloneInlines(next.Inlines));
            Document.Blocks.RemoveAt(position.BlockIndex + 1);
            block.Normalize();
            Selection = RichTextSelection.Collapsed(position);
        });
        return true;
    }

    public void InsertParagraph()
    {
        Execute(() =>
        {
            DeleteSelectionCore();
            var position = SplitBlockCore(Selection.Start);
            Selection = RichTextSelection.Collapsed(position);
        });
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Push(Capture());
        Restore(_undo.Pop());
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Push(Capture());
        Restore(_redo.Pop());
        return true;
    }

    public void ToggleMarks(RichTextMarks marks)
    {
        if (Selection.IsCollapsed)
        {
            ActiveMarks = MergeMarks(ActiveMarks, marks, ResolveToggleMarks(ActiveMarks, marks));
            return;
        }
        Execute(() => ApplyMarksCore(Selection, marks, ResolveToggleMarks(Selection, marks)));
    }

    public void SetMarks(RichTextMarks marks)
    {
        if (Selection.IsCollapsed)
        {
            ActiveMarks = marks;
            return;
        }
        Execute(() => SetMarksCore(Selection, marks));
    }

    public RichTextFragment GetSelectedFragment()
    {
        if (Selection.IsCollapsed) return new RichTextFragment();
        var start = Selection.Start;
        var end = Selection.End;
        var blocks = new List<RichTextBlock>();
        for (var blockIndex = start.BlockIndex; blockIndex <= end.BlockIndex; blockIndex++)
        {
            var block = Document.Blocks[blockIndex];
            var blockStart = blockIndex == start.BlockIndex ? start.Offset : 0;
            var blockEnd = blockIndex == end.BlockIndex ? end.Offset : block.PlainText.Length;
            blocks.Add(new RichTextBlock(
                block.Kind,
                SliceBlock(block, blockStart, blockEnd - blockStart),
                block.HeadingLevel));
        }
        return new RichTextFragment(blocks);
    }

    public void InsertFragment(RichTextFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (fragment.Blocks.Count == 0) return;

        Execute(() =>
        {
            DeleteSelectionCore();
            var position = Selection.Start;
            var target = Document.Blocks[position.BlockIndex];
            var before = SliceBlock(target, 0, position.Offset);
            var after = SliceBlock(target, position.Offset, target.PlainText.Length - position.Offset);
            var inserted = CloneBlocks(fragment.Blocks);

            target.Inlines.Clear();
            target.Inlines.AddRange(before);
            target.Inlines.AddRange(CloneInlines(inserted[0].Inlines));
            target.Normalize();

            if (inserted.Count == 1)
            {
                var offset = target.PlainText.Length;
                target.Inlines.AddRange(after);
                target.Normalize();
                Selection = RichTextSelection.Collapsed(new RichTextPosition(position.BlockIndex, offset));
                return;
            }

            var insertIndex = position.BlockIndex + 1;
            for (var i = 1; i < inserted.Count - 1; i++)
                Document.Blocks.Insert(insertIndex++, inserted[i]);

            var last = inserted[^1];
            var caretOffset = last.PlainText.Length;
            last.Inlines.AddRange(after);
            last.Normalize();
            Document.Blocks.Insert(insertIndex, last);
            Selection = RichTextSelection.Collapsed(new RichTextPosition(insertIndex, caretOffset));
        });
    }

    private void Execute(Action mutation)
    {
        var before = Capture();
        mutation();
        Document.Normalize();
        _undo.Push(before);
        _redo.Clear();
    }

    private void DeleteSelectionCore()
    {
        if (Selection.IsCollapsed) return;
        var start = Selection.Start;
        DeleteRangeCore(Selection);
        Selection = RichTextSelection.Collapsed(start);
    }

    private void DeleteRangeCore(RichTextSelection selection)
    {
        var start = selection.Start;
        var end = selection.End;
        ValidatePosition(start);
        ValidatePosition(end);

        if (start.BlockIndex == end.BlockIndex)
        {
            var block = Document.Blocks[start.BlockIndex];
            var before = SliceBlock(block, 0, start.Offset);
            var after = SliceBlock(block, end.Offset, block.PlainText.Length - end.Offset);
            block.Inlines.Clear();
            block.Inlines.AddRange(before);
            block.Inlines.AddRange(after);
            block.Normalize();
            return;
        }

        var startBlock = Document.Blocks[start.BlockIndex];
        var endBlock = Document.Blocks[end.BlockIndex];
        var merged = new List<RichTextInline>();
        merged.AddRange(SliceBlock(startBlock, 0, start.Offset));
        merged.AddRange(SliceBlock(endBlock, end.Offset, endBlock.PlainText.Length - end.Offset));
        startBlock.Inlines.Clear();
        startBlock.Inlines.AddRange(merged);
        Document.Blocks.RemoveRange(start.BlockIndex + 1, end.BlockIndex - start.BlockIndex);
        startBlock.Normalize();
    }

    private void InsertTextCore(RichTextPosition position, string text, RichTextMarks marks)
    {
        ValidatePosition(position);
        var block = Document.Blocks[position.BlockIndex];
        var next = new List<RichTextInline>();
        next.AddRange(SliceBlock(block, 0, position.Offset));
        next.Add(new RichTextRun(text, marks));
        next.AddRange(SliceBlock(block, position.Offset, block.PlainText.Length - position.Offset));
        block.Inlines.Clear();
        block.Inlines.AddRange(next);
        block.Normalize();
    }

    private void ApplyMarksCore(RichTextSelection selection, RichTextMarks requested, RichTextMarks target)
    {
        var start = selection.Start;
        var end = selection.End;
        for (var blockIndex = start.BlockIndex; blockIndex <= end.BlockIndex; blockIndex++)
        {
            var block = Document.Blocks[blockIndex];
            var blockStart = blockIndex == start.BlockIndex ? start.Offset : 0;
            var blockEnd = blockIndex == end.BlockIndex ? end.Offset : block.PlainText.Length;
            var before = SliceBlock(block, 0, blockStart);
            var middle = SliceBlock(block, blockStart, blockEnd - blockStart)
                .Select(inline => inline is RichTextRun run ? new RichTextRun(run.Text, MergeMarks(run.Marks, requested, target)) : inline)
                .ToList();
            var after = SliceBlock(block, blockEnd, block.PlainText.Length - blockEnd);
            block.Inlines.Clear();
            block.Inlines.AddRange(before);
            block.Inlines.AddRange(middle);
            block.Inlines.AddRange(after);
            block.Normalize();
        }
    }

    private void SetMarksCore(RichTextSelection selection, RichTextMarks marks)
    {
        var start = selection.Start;
        var end = selection.End;
        for (var blockIndex = start.BlockIndex; blockIndex <= end.BlockIndex; blockIndex++)
        {
            var block = Document.Blocks[blockIndex];
            var blockStart = blockIndex == start.BlockIndex ? start.Offset : 0;
            var blockEnd = blockIndex == end.BlockIndex ? end.Offset : block.PlainText.Length;
            var before = SliceBlock(block, 0, blockStart);
            var middle = SliceBlock(block, blockStart, blockEnd - blockStart)
                .Select(inline => inline is RichTextRun run ? new RichTextRun(run.Text, marks) : inline)
                .ToList();
            var after = SliceBlock(block, blockEnd, block.PlainText.Length - blockEnd);
            block.Inlines.Clear();
            block.Inlines.AddRange(before);
            block.Inlines.AddRange(middle);
            block.Inlines.AddRange(after);
            block.Normalize();
        }
    }

    private RichTextMarks ResolveToggleMarks(RichTextSelection selection, RichTextMarks requested)
    {
        var runs = EnumerateSelectedRuns(selection).ToArray();
        return requested with
        {
            Bold = requested.Bold && !runs.All(run => run.Marks.Bold),
            Italic = requested.Italic && !runs.All(run => run.Marks.Italic),
            Underline = requested.Underline && !runs.All(run => run.Marks.Underline)
        };
    }

    private static RichTextMarks ResolveToggleMarks(RichTextMarks current, RichTextMarks requested) => requested with
    {
        Bold = requested.Bold && !current.Bold,
        Italic = requested.Italic && !current.Italic,
        Underline = requested.Underline && !current.Underline
    };

    private RichTextMarks ResolveMarksAt(RichTextPosition position)
    {
        var block = Document.Blocks[position.BlockIndex];
        var cursor = 0;
        RichTextMarks? previous = null;
        foreach (var inline in block.Inlines)
        {
            if (inline is not RichTextRun run) continue;
            var end = cursor + run.Text.Length;
            if (position.Offset < end) return run.Marks;
            previous = run.Marks;
            cursor = end;
        }
        return previous ?? RichTextMarks.Empty;
    }

    private IEnumerable<RichTextRun> EnumerateSelectedRuns(RichTextSelection selection)
    {
        var start = selection.Start;
        var end = selection.End;
        for (var blockIndex = start.BlockIndex; blockIndex <= end.BlockIndex; blockIndex++)
        {
            var block = Document.Blocks[blockIndex];
            var blockStart = blockIndex == start.BlockIndex ? start.Offset : 0;
            var blockEnd = blockIndex == end.BlockIndex ? end.Offset : block.PlainText.Length;
            var cursor = 0;
            foreach (var inline in block.Inlines)
            {
                if (inline is not RichTextRun run) continue;
                var runStart = cursor;
                var runEnd = cursor + run.Text.Length;
                cursor = runEnd;
                if (runEnd <= blockStart || runStart >= blockEnd) continue;
                yield return run;
            }
        }
    }

    private static RichTextMarks MergeMarks(RichTextMarks current, RichTextMarks requested, RichTextMarks target) => current with
    {
        Bold = requested.Bold ? target.Bold : current.Bold,
        Italic = requested.Italic ? target.Italic : current.Italic,
        Underline = requested.Underline ? target.Underline : current.Underline,
        Link = requested.Link ?? current.Link,
        Foreground = requested.Foreground ?? current.Foreground,
        Background = requested.Background ?? current.Background
    };

    private RichTextPosition SplitBlockCore(RichTextPosition position)
    {
        ValidatePosition(position);
        var block = Document.Blocks[position.BlockIndex];
        var before = SliceBlock(block, 0, position.Offset);
        var after = SliceBlock(block, position.Offset, block.PlainText.Length - position.Offset);
        block.Inlines.Clear();
        block.Inlines.AddRange(before);
        block.Normalize();

        var next = new RichTextBlock(RichTextBlockKind.Paragraph, after);
        Document.Blocks.Insert(position.BlockIndex + 1, next);
        return new RichTextPosition(position.BlockIndex + 1, 0);
    }

    private static List<RichTextInline> SliceBlock(RichTextBlock block, int start, int length)
    {
        if (length == 0) return [];
        var end = start + length;
        var cursor = 0;
        var result = new List<RichTextInline>();
        foreach (var inline in block.Inlines)
        {
            if (inline is not RichTextRun run) continue;
            var runStart = cursor;
            var runEnd = cursor + run.Text.Length;
            cursor = runEnd;
            if (runEnd <= start || runStart >= end) continue;
            var sliceStart = Math.Max(start, runStart) - runStart;
            var sliceEnd = Math.Min(end, runEnd) - runStart;
            result.Add(run.Slice(sliceStart, sliceEnd - sliceStart));
        }
        return result;
    }

    private void ValidatePosition(RichTextPosition position)
    {
        if (position.BlockIndex < 0 || position.BlockIndex >= Document.Blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(position.BlockIndex));
        var blockLength = Document.Blocks[position.BlockIndex].PlainText.Length;
        if (position.Offset < 0 || position.Offset > blockLength)
            throw new ArgumentOutOfRangeException(nameof(position.Offset));
    }

    private Snapshot Capture() => new(CloneBlocks(Document.Blocks), Selection, ActiveMarks);

    private void Restore(Snapshot snapshot)
    {
        Document.Blocks.Clear();
        Document.Blocks.AddRange(CloneBlocks(snapshot.Blocks));
        Selection = snapshot.Selection;
        ActiveMarks = snapshot.ActiveMarks;
    }

    private static List<RichTextBlock> CloneBlocks(IEnumerable<RichTextBlock> blocks) =>
        blocks.Select(block => new RichTextBlock(block.Kind, CloneInlines(block.Inlines), block.HeadingLevel)).ToList();

    private static List<RichTextInline> CloneInlines(IEnumerable<RichTextInline> inlines) =>
        inlines.Select(inline => inline switch
        {
            RichTextRun run => new RichTextRun(run.Text, run.Marks),
            _ => throw new InvalidOperationException($"Unsupported rich text inline '{inline.GetType().Name}'.")
        }).Cast<RichTextInline>().ToList();

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private sealed record Snapshot(List<RichTextBlock> Blocks, RichTextSelection Selection, RichTextMarks ActiveMarks);
}
