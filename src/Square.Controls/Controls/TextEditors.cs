using System.Text;
using Square.Graphics;
using Square.Text.Glyph;
using Square.UI;

namespace Square.Controls.Controls;

public interface ITextEditor
{
    int CaretIndex { get; }
    int SelectionStart { get; }
    int SelectionLength { get; }
    string SelectedText { get; }
    Rect CaretRect { get; }

    void HandleTextInput(string text);
    void HandleKey(int keyCode, bool shift = false, bool control = false);
    void HandlePointerDown(Point point, bool extendSelection = false);
    void HandlePointerMove(Point point);
    void HandlePointerUp(Point point);
    void SelectAll();
    bool DeleteSelection();
    bool ToggleCaretBlink();
    void ResetCaretBlink();
}

public abstract class TextEditorBase : UIElement, ITextEditor
{
    private const float DefaultFontSize = 14f;
    private const float ContentPaddingX = 8f;
    private const float ContentPaddingY = 8f;
    private static readonly SystemGlyphRasterizer GlyphRasterizer = new();
    private int _caretIndex;
    private int _selectionAnchor;
    private bool _isDragging;
    private float _horizontalScroll;
    private float? _preferredX;
    private bool _caretVisible = true;

    protected abstract bool IsMultiline { get; }

    protected TextEditorBase()
    {
        AddEventListener("focus", ResetCaretBlink);
        AddEventListener("blur", CollapseSelectionOnBlur);
    }

    public string Value
    {
        get => GetProperty<string>(nameof(Value)) ?? "";
        set => SetProperty(nameof(Value), NormalizeNewlines(value ?? ""));
    }

    public string Placeholder
    {
        get => GetProperty<string>(nameof(Placeholder)) ?? "";
        set => SetProperty(nameof(Placeholder), value);
    }

    public int CaretIndex => _caretIndex;
    public int SelectionStart => Math.Min(_caretIndex, _selectionAnchor);
    public int SelectionLength => Math.Abs(_caretIndex - _selectionAnchor);
    public string SelectedText => SelectionLength == 0 ? "" : Value.Substring(SelectionStart, SelectionLength);
    public Rect CaretRect => GetCaretRect();
    public Color SelectionBackground
    {
        get => Properties.HasValue(nameof(SelectionBackground))
            ? GetProperty<Color>(nameof(SelectionBackground))
            : Color.FromRgb(51, 144, 255);
        set => SetProperty(nameof(SelectionBackground), value);
    }
    public Color SelectionForeground
    {
        get => Properties.HasValue(nameof(SelectionForeground)) ? GetProperty<Color>(nameof(SelectionForeground)) : Color.White;
        set => SetProperty(nameof(SelectionForeground), value);
    }

    public override void Render(IRenderContext context)
    {
        ControlDrawing.DrawInputFrame(context, this);
        EnsureCaretVisible();
        context.PushClip(new Rect(Geometry.X + 1, Geometry.Y + 1, Math.Max(0, Geometry.Width - 2), Math.Max(0, Geometry.Height - 2)));

        var fontSize = GetFontSize();
        var lineHeight = GetLineHeight(fontSize);
        var textColor = ControlDrawing.GetStyledColor(this, "color", Color.Black);
        var selectionBackground = ControlDrawing.GetStyledColor(this, "selection-background", SelectionBackground);
        var selectionForeground = ControlDrawing.GetStyledColor(this, "selection-color", SelectionForeground);

        if (string.IsNullOrEmpty(Value))
        {
            if (!string.IsNullOrEmpty(Placeholder))
                ControlDrawing.DrawText(
                    context, this, Placeholder, GetTextOrigin(fontSize, lineHeight),
                    Color.FromRgb(125, 130, 136), fontSize, lineHeight);
        }
        else
        {
            var selectionRects = GetSelectionRects();
            foreach (var rect in selectionRects)
                context.FillRect(rect, new SolidColorBrush(selectionBackground));
            ControlDrawing.DrawText(context, this, Value, GetTextOrigin(fontSize, lineHeight), textColor, fontSize, lineHeight);
            foreach (var rect in selectionRects)
            {
                context.PushClip(rect);
                ControlDrawing.DrawText(
                    context, this, Value, GetTextOrigin(fontSize, lineHeight),
                    selectionForeground, fontSize, lineHeight, useStyledColor: false);
                context.PopClip();
            }
        }

        if (IsFocused && SelectionLength == 0 && _caretVisible)
            context.FillRect(CaretRect, new SolidColorBrush(ControlDrawing.GetStyledColor(this, "caret-color", textColor)));

        context.PopClip();
    }

    public void HandleTextInput(string text)
    {
        if (!IsEnabled || string.IsNullOrEmpty(text)) return;
        text = NormalizeNewlines(text);
        if (!IsMultiline) text = text.Replace("\n", "");
        if (text.Length == 0) return;
        ReplaceSelection(text);
    }

    public void HandleKey(int keyCode, bool shift = false, bool control = false)
    {
        if (!IsEnabled) return;
        switch (keyCode)
        {
            case 8:
                Backspace();
                return;
            case 13 when IsMultiline:
                ReplaceSelection("\n");
                return;
            case 35:
                MoveCaret(control ? Value.Length : CurrentLine().End, shift);
                return;
            case 36:
                MoveCaret(control ? 0 : CurrentLine().Start, shift);
                return;
            case 37:
                MoveHorizontal(-1, shift, control);
                return;
            case 38 when IsMultiline:
                MoveVertical(-1, shift);
                return;
            case 39:
                MoveHorizontal(1, shift, control);
                return;
            case 40 when IsMultiline:
                MoveVertical(1, shift);
                return;
            case 38 or 40:
                return;
            case 46:
                DeleteForward();
                return;
            case 65 when control:
                SelectAll();
                return;
        }

        if (!control && keyCode is >= 32 and <= 126)
            HandleTextInput(((char)keyCode).ToString());
    }

    public void HandlePointerDown(Point point, bool extendSelection = false)
    {
        if (!IsEnabled) return;
        var index = HitTestIndex(point);
        if (!extendSelection) _selectionAnchor = index;
        _caretIndex = index;
        _isDragging = true;
        _preferredX = null;
        ResetCaretBlink();
        InvalidateVisual();
    }

    public void HandlePointerMove(Point point)
    {
        if (!_isDragging) return;
        _caretIndex = HitTestIndex(point);
        _preferredX = null;
        ResetCaretBlink();
        InvalidateVisual();
    }

    public void HandlePointerUp(Point point)
    {
        if (!_isDragging) return;
        _caretIndex = HitTestIndex(point);
        _isDragging = false;
        _preferredX = null;
        ResetCaretBlink();
        InvalidateVisual();
    }

    public void SelectAll()
    {
        _selectionAnchor = 0;
        _caretIndex = Value.Length;
        _preferredX = null;
        ResetCaretBlink();
        InvalidateVisual();
    }

    public bool DeleteSelection()
    {
        if (SelectionLength == 0) return false;
        ReplaceSelection("");
        return true;
    }

    public bool ToggleCaretBlink()
    {
        if (!IsFocused || SelectionLength > 0) return false;
        _caretVisible = !_caretVisible;
        InvalidateVisual();
        return true;
    }

    public void ResetCaretBlink()
    {
        _caretVisible = true;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name != nameof(Value)) return;
        if (!IsFocused)
        {
            _caretIndex = Value.Length;
            _selectionAnchor = _caretIndex;
        }
        else
        {
            _caretIndex = Math.Clamp(_caretIndex, 0, Value.Length);
            _selectionAnchor = Math.Clamp(_selectionAnchor, 0, Value.Length);
        }
    }

    private void ReplaceSelection(string replacement)
    {
        var start = SelectionStart;
        var length = SelectionLength;
        Value = Value.Remove(start, length).Insert(start, replacement);
        _caretIndex = start + replacement.Length;
        _selectionAnchor = _caretIndex;
        _preferredX = null;
        ResetCaretBlink();
        EnsureCaretVisible();
        RaiseEvent("input");
        InvalidateVisual();
    }

    private void Backspace()
    {
        if (DeleteSelection()) return;
        if (_caretIndex == 0) return;
        var previous = PreviousCodePointIndex(Value, _caretIndex);
        _selectionAnchor = previous;
        ReplaceSelection("");
    }

    private void DeleteForward()
    {
        if (DeleteSelection()) return;
        if (_caretIndex >= Value.Length) return;
        _selectionAnchor = NextCodePointIndex(Value, _caretIndex);
        ReplaceSelection("");
    }

    private void MoveHorizontal(int direction, bool extend, bool byWord)
    {
        if (!extend && SelectionLength > 0)
        {
            MoveCaret(direction < 0 ? SelectionStart : SelectionStart + SelectionLength, false);
            return;
        }
        var target = direction < 0
            ? byWord ? PreviousWordIndex(Value, _caretIndex) : PreviousCodePointIndex(Value, _caretIndex)
            : byWord ? NextWordIndex(Value, _caretIndex) : NextCodePointIndex(Value, _caretIndex);
        MoveCaret(target, extend);
    }

    private void MoveVertical(int direction, bool extend)
    {
        var lines = GetLines(Value);
        var currentLineIndex = FindLineIndex(lines, _caretIndex);
        var targetLineIndex = Math.Clamp(currentLineIndex + direction, 0, lines.Count - 1);
        if (targetLineIndex == currentLineIndex) return;
        var current = lines[currentLineIndex];
        var target = lines[targetLineIndex];
        _preferredX ??= MeasureRange(Value, current.Start, Math.Max(0, _caretIndex - current.Start));
        var targetIndex = target.Start + HitTestLine(Value.AsSpan(target.Start, target.Length), _preferredX.Value);
        MoveCaret(targetIndex, extend, preservePreferredX: true);
    }

    private void MoveCaret(int index, bool extend, bool preservePreferredX = false)
    {
        _caretIndex = Math.Clamp(index, 0, Value.Length);
        if (!extend) _selectionAnchor = _caretIndex;
        if (!preservePreferredX) _preferredX = null;
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private int HitTestIndex(Point point)
    {
        var lines = GetLines(Value);
        var lineHeight = GetLineHeight(GetFontSize());
        var lineIndex = IsMultiline
            ? Math.Clamp((int)MathF.Floor((point.Y - GetFirstLineTop(lineHeight)) / lineHeight), 0, lines.Count - 1)
            : 0;
        var line = lines[lineIndex];
        var localX = point.X - Geometry.X - ContentPaddingX + _horizontalScroll;
        return line.Start + HitTestLine(Value.AsSpan(line.Start, line.Length), localX);
    }

    private List<Rect> GetSelectionRects()
    {
        var result = new List<Rect>();
        if (SelectionLength == 0) return result;
        var fontSize = GetFontSize();
        var lineHeight = GetLineHeight(fontSize);
        var origin = GetTextOrigin(fontSize, lineHeight);
        var lines = GetLines(Value);
        var selectionEnd = SelectionStart + SelectionLength;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var start = Math.Max(SelectionStart, line.Start);
            var end = Math.Min(selectionEnd, line.End);
            var includesNewline = i < lines.Count - 1 && selectionEnd > line.End;
            if (end < start || end == start && !includesNewline) continue;
            var x = MeasureRange(Value, line.Start, start - line.Start);
            var width = MeasureRange(Value, start, end - start);
            if (includesNewline) width += 6;
            var visualLineBox = GetVisualLineBox(fontSize, lineHeight, i);
            result.Add(new Rect(
                origin.X + x,
                visualLineBox.Top,
                Math.Max(2, width),
                visualLineBox.Height));
        }
        return result;
    }

    private Rect GetCaretRect()
    {
        EnsureCaretVisible();
        var fontSize = GetFontSize();
        var lineHeight = GetLineHeight(fontSize);
        var lines = GetLines(Value);
        var lineIndex = FindLineIndex(lines, _caretIndex);
        var line = lines[lineIndex];
        var width = MeasureRange(Value, line.Start, Math.Max(0, _caretIndex - line.Start));
        var visualLineBox = GetVisualLineBox(fontSize, lineHeight, lineIndex);
        var inset = Math.Min(2f, Math.Max(0, (visualLineBox.Height - 1) / 2));
        return new Rect(
            MathF.Round(Geometry.X + ContentPaddingX - _horizontalScroll + width),
            MathF.Round(visualLineBox.Top + inset),
            1,
            Math.Max(1, Math.Min(
                visualLineBox.Height - inset * 2,
                Geometry.Bottom - visualLineBox.Top - inset - 1)));
    }

    private Point GetTextOrigin(float fontSize, float lineHeight)
    {
        var naturalLineHeight = MathF.Round(fontSize * TextLayout.DefaultLineHeight);
        var textOffset = (lineHeight - naturalLineHeight) / 2f;
        return new Point(
            Geometry.X + ContentPaddingX - _horizontalScroll,
            GetFirstLineTop(lineHeight) + textOffset);
    }

    private float GetFirstLineTop(float lineHeight) => IsMultiline
        ? Geometry.Y + ContentPaddingY
        : Geometry.Y + Math.Max(1, (Geometry.Height - lineHeight) / 2f);

    private (float Top, float Height) GetVisualLineBox(float fontSize, float lineHeight, int lineIndex)
    {
        var naturalLineHeight = MathF.Round(fontSize * TextLayout.DefaultLineHeight);
        var visualHeight = Math.Max(lineHeight, naturalLineHeight);
        var lineTop = GetFirstLineTop(lineHeight) + lineIndex * lineHeight;
        return (lineTop + (lineHeight - visualHeight) / 2f, visualHeight);
    }

    private float GetFontSize() => ControlDrawing.GetStyledFloat(this, "font-size", DefaultFontSize);

    private float GetLineHeight(float fontSize) => ControlDrawing.GetStyledLineHeight(this, fontSize);

    private void EnsureCaretVisible()
    {
        if (IsMultiline)
        {
            _horizontalScroll = 0;
            return;
        }
        var width = MeasureRange(Value, 0, _caretIndex);
        var viewport = Math.Max(0, Geometry.Width - ContentPaddingX * 2 - 2);
        if (width - _horizontalScroll > viewport) _horizontalScroll = width - viewport;
        if (width - _horizontalScroll < 0) _horizontalScroll = width;
        _horizontalScroll = Math.Max(0, _horizontalScroll);
    }

    private LineRange CurrentLine()
    {
        var lines = GetLines(Value);
        return lines[FindLineIndex(lines, _caretIndex)];
    }

    private static List<LineRange> GetLines(string text)
    {
        var lines = new List<LineRange>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            lines.Add(new LineRange(start, i - start));
            start = i + 1;
        }
        lines.Add(new LineRange(start, text.Length - start));
        return lines;
    }

    private static int FindLineIndex(List<LineRange> lines, int index)
    {
        for (var i = 0; i < lines.Count; i++)
            if (index <= lines[i].End || i == lines.Count - 1) return i;
        return lines.Count - 1;
    }

    private int HitTestLine(ReadOnlySpan<char> line, float x)
    {
        if (x <= 0) return 0;
        var width = 0f;
        var index = 0;
        while (index < line.Length)
        {
            var length = char.IsHighSurrogate(line[index]) && index + 1 < line.Length && char.IsLowSurrogate(line[index + 1]) ? 2 : 1;
            var advance = MeasureCharacterAdvance(line.Slice(index, length));
            if (x < width + advance / 2f) return index;
            width += advance;
            index += length;
        }
        return line.Length;
    }

    private float MeasureRange(string text, int start, int length)
    {
        if (length <= 0) return 0;
        var width = 0f;
        var span = text.AsSpan(start, length);
        var index = 0;
        while (index < span.Length)
        {
            var characterLength = char.IsHighSurrogate(span[index]) && index + 1 < span.Length && char.IsLowSurrogate(span[index + 1]) ? 2 : 1;
            width += MeasureCharacterAdvance(span.Slice(index, characterLength));
            index += characterLength;
        }
        return width;
    }

    private float MeasureCharacterAdvance(ReadOnlySpan<char> character)
    {
        var fontSize = GetFontSize();
        var editorFont = new Font("Segoe UI", fontSize);
        if (character.Length == 1)
        {
            var glyph = GlyphRasterizer.Rasterize(editorFont, character[0]);
            if (glyph != null) return glyph.AdvanceX;
            if (char.IsSurrogate(character[0])) return fontSize * 0.5f;
            return TextLayout.MeasureRuneAdvance(new Rune(character[0]), fontSize);
        }

        Rune.DecodeFromUtf16(character, out var rune, out _);
        return TextLayout.MeasureRuneAdvance(rune, fontSize);
    }

    private static int PreviousCodePointIndex(string text, int index)
    {
        if (index <= 0) return 0;
        index--;
        if (index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1])) index--;
        return index;
    }

    private static int NextCodePointIndex(string text, int index)
    {
        if (index >= text.Length) return text.Length;
        return index + (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1);
    }

    private static int PreviousWordIndex(string text, int index)
    {
        while (index > 0 && char.IsWhiteSpace(text[index - 1])) index--;
        while (index > 0 && !char.IsWhiteSpace(text[index - 1])) index--;
        return index;
    }

    private static int NextWordIndex(string text, int index)
    {
        while (index < text.Length && !char.IsWhiteSpace(text[index])) index++;
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private void CollapseSelectionOnBlur()
    {
        _selectionAnchor = _caretIndex;
        _isDragging = false;
        _caretVisible = false;
        InvalidateVisual();
    }

    private readonly record struct LineRange(int Start, int Length)
    {
        internal int End => Start + Length;
    }
}

public class Input : TextEditorBase
{
    protected override bool IsMultiline => false;
    public override Size Measure(Size availableSize) => new(200, 36);
}

public class TextArea : TextEditorBase
{
    protected override bool IsMultiline => true;
    public override Size Measure(Size availableSize) => new(300, 88);
}
