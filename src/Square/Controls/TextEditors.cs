using System.Text;
using Square.Controls.Animation;
using Square.Events;
using Square.Graphics;
using Square.UI;

namespace Square.Controls;

public interface ITextEditor
{
    int CaretIndex { get; }
    int SelectionStart { get; }
    int SelectionLength { get; }
    string SelectedText { get; }
    bool CanCopySelection { get; }
    bool CanCutSelection { get; }
    Rect CaretRect { get; }

    void HandleTextInput(string text);
    void HandleKey(int keyCode, bool shift = false, bool control = false);
    void HandlePointerDown(Point point, bool extendSelection = false);
    void HandlePointerMove(Point point);
    void HandlePointerUp(Point point);
    void SelectWordAt(Point point);
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
    private int _caretIndex;
    private int _selectionAnchor;
    private bool _isDragging;
    private float _horizontalScroll;
    private float? _preferredX;
    private float _caretOpacity = 1f;
    private float _caretBlinkTarget;
    private double _nextCaretTransitionSeconds;
    private Animation<float>? _caretBlinkAnimation;
    private readonly System.Diagnostics.Stopwatch _caretClock = System.Diagnostics.Stopwatch.StartNew();

    protected abstract bool IsMultiline { get; }
    protected virtual bool CanEditText => true;
    protected virtual bool PaintEditorChrome => true;
    protected virtual bool ShowCaret => true;
    protected virtual float TextPaddingX => ContentPaddingX;
    protected virtual float TextPaddingY => ContentPaddingY;

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

    protected virtual string DisplayValue => Value;

    public int CaretIndex => _caretIndex;
    public int SelectionStart => Math.Min(_caretIndex, _selectionAnchor);
    public int SelectionLength => Math.Abs(_caretIndex - _selectionAnchor);
    public string SelectedText => SelectionLength == 0 ? "" : Value.Substring(SelectionStart, SelectionLength);
    public virtual bool CanCopySelection => true;
    public virtual bool CanCutSelection => true;
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

    public override void Paint(IRenderContext context)
    {
        if (PaintEditorChrome) ControlDrawing.DrawInputFrame(context, this);
        EnsureCaretVisible();
        context.PushClip(PaintEditorChrome
            ? new Rect(Geometry.X + 1, Geometry.Y + 1, Math.Max(0, Geometry.Width - 2), Math.Max(0, Geometry.Height - 2))
            : Geometry);

        var fontSize = GetFontSize();
        var lineHeight = GetLineHeight(fontSize);
        var textColor = ControlDrawing.GetStyledColor(this, "color", Color.Black);
        var selectionBackground = ControlDrawing.GetStyledColor(
            this,
            Style.Get("selection-background-color") != null ? "selection-background-color" : "selection-background",
            SelectionBackground);
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
            var displayValue = DisplayValue;
            var selectionRects = GetSelectionRects(displayValue);
            ControlDrawing.DrawText(context, this, displayValue, GetTextOrigin(fontSize, lineHeight), textColor, fontSize, lineHeight);
            foreach (var rect in selectionRects)
                context.FillRect(rect, new SolidColorBrush(selectionBackground));
            foreach (var rect in selectionRects)
            {
                context.PushClip(rect);
                ControlDrawing.DrawText(
                    context, this, displayValue, GetTextOrigin(fontSize, lineHeight),
                    selectionForeground, fontSize, lineHeight, useStyledColor: false);
                context.PopClip();
            }
        }

        if (ShowCaret && IsFocused && SelectionLength == 0 && _caretOpacity > 0.01f)
        {
            var caretColor = ControlDrawing.GetStyledColor(this, "caret-color", textColor);
            context.FillRect(CaretRect, new SolidColorBrush(Color.FromRgba(caretColor.R, caretColor.G, caretColor.B, (byte)Math.Clamp(_caretOpacity * 255f, 0f, 255f))));
        }

        context.PopClip();
    }

    public void HandleTextInput(string text)
    {
        if (!CanEditText || !IsEnabled || string.IsNullOrEmpty(text)) return;
        text = NormalizeNewlines(text);
        if (!IsMultiline) text = text.Replace("\n", "");
        text = FilterInput(text);
        if (text.Length == 0) return;
        ReplaceSelection(text);
    }

    protected virtual string FilterInput(string text) => text;

    public void HandleKey(int keyCode, bool shift = false, bool control = false)
    {
        if (!IsEnabled) return;
        switch (keyCode)
        {
            case 8 when CanEditText:
                Backspace();
                return;
            case 13 when CanEditText && IsMultiline:
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
            case 46 when CanEditText:
                DeleteForward();
                return;
            case 65 when control:
                SelectAll();
                return;
            case 88 when CanEditText && control:
                DeleteSelection();
                return;
        }
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
        InvalidatePaint();
    }

    public void HandlePointerMove(Point point)
    {
        if (!_isDragging) return;
        _caretIndex = HitTestIndex(point);
        _preferredX = null;
        ResetCaretBlink();
        InvalidatePaint();
    }

    public void HandlePointerUp(Point point)
    {
        if (!_isDragging) return;
        _caretIndex = HitTestIndex(point);
        _isDragging = false;
        _preferredX = null;
        ResetCaretBlink();
        InvalidatePaint();
    }

    public void SelectWordAt(Point point)
    {
        var index = HitTestIndex(point);
        var (start, end) = FindWordAt(Value, index);
        _selectionAnchor = start;
        _caretIndex = end;
        _isDragging = false;
        _preferredX = null;
        ResetCaretBlink();
        InvalidatePaint();
    }

    public void SelectAll()
    {
        _selectionAnchor = 0;
        _caretIndex = Value.Length;
        _preferredX = null;
        ResetCaretBlink();
        InvalidatePaint();
    }

    public bool DeleteSelection()
    {
        if (!CanEditText || SelectionLength == 0) return false;
        ReplaceSelection("");
        return true;
    }

    public bool ToggleCaretBlink()
    {
        if (!IsFocused || SelectionLength > 0) return false;
        var now = _caretClock.Elapsed.TotalSeconds;
        if ((_caretBlinkAnimation == null || _caretBlinkAnimation.IsComplete) && now < _nextCaretTransitionSeconds)
            return false;
        if (_caretBlinkAnimation == null || _caretBlinkAnimation.IsComplete)
        {
            _caretBlinkTarget = _caretOpacity > 0.5f ? 0f : 1f;
            _caretBlinkAnimation = CreateCaretBlinkAnimation(_caretOpacity, _caretBlinkTarget);
            _caretBlinkAnimation.Start();
        }
        _caretBlinkAnimation.Update(1f / 30f);
        if (_caretBlinkAnimation.IsComplete)
            _nextCaretTransitionSeconds = now + (_caretBlinkTarget <= 0.01f ? 0.45d : 0.7d);
        InvalidatePaint();
        return true;
    }

    public void ResetCaretBlink()
    {
        _caretOpacity = 1f;
        _caretBlinkTarget = 0f;
        _nextCaretTransitionSeconds = _caretClock.Elapsed.TotalSeconds + 0.7d;
        _caretBlinkAnimation = null;
        InvalidatePaint();
    }

    private Animation<float> CreateCaretBlinkAnimation(float from, float to) => new(
        Interpolate,
        from,
        to,
        0.28f,
        t => t,
        value => _caretOpacity = value);

    private static float Interpolate(float from, float to, float t) => from + (to - from) * t;

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
        DispatchEvent(StandardEvents.CreateInput());
        InvalidatePaint();
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
        InvalidatePaint();
    }

    private int HitTestIndex(Point point)
    {
        var displayValue = DisplayValue;
        var lines = GetLines(displayValue);
        var lineHeight = GetLineHeight(GetFontSize());
        var lineIndex = IsMultiline
            ? Math.Clamp((int)MathF.Floor((point.Y - GetFirstLineTop(lineHeight)) / lineHeight), 0, lines.Count - 1)
            : 0;
        var line = lines[lineIndex];
        var localX = point.X - Geometry.X - TextPaddingX + _horizontalScroll;
        return line.Start + HitTestLine(displayValue.AsSpan(line.Start, line.Length), localX);
    }

    private List<Rect> GetSelectionRects(string displayValue)
    {
        var result = new List<Rect>();
        if (SelectionLength == 0) return result;
        var fontSize = GetFontSize();
        var lineHeight = GetLineHeight(fontSize);
        var origin = GetTextOrigin(fontSize, lineHeight);
        var lines = GetLines(displayValue);
        var selectionEnd = SelectionStart + SelectionLength;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var start = Math.Max(SelectionStart, line.Start);
            var end = Math.Min(selectionEnd, line.End);
            var includesNewline = i < lines.Count - 1 && selectionEnd > line.End;
            if (end < start || end == start && !includesNewline) continue;
            var x = MeasureRange(displayValue, line.Start, start - line.Start);
            var width = MeasureRange(displayValue, start, end - start);
            if (end > start)
            {
                var font = ControlDrawing.ResolveFont(this, fontSize);
                var firstRune = DecodeRuneAt(displayValue, start);
                var lastRune = DecodeRuneBefore(displayValue, end);
                var firstInk = ControlDrawing.MeasureRenderedRuneInkBounds(firstRune, font);
                var lastInk = ControlDrawing.MeasureRenderedRuneInkBounds(lastRune, font);
                x += firstInk.Left;
                width += lastInk.Right - MeasureCharacterAdvanceBefore(displayValue, end) - firstInk.Left;
            }
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
        var displayValue = DisplayValue;
        var lines = GetLines(displayValue);
        var lineIndex = FindLineIndex(lines, _caretIndex);
        var line = lines[lineIndex];
        var width = MeasureRange(displayValue, line.Start, Math.Max(0, _caretIndex - line.Start));
        var visualLineBox = GetVisualLineBox(fontSize, lineHeight, lineIndex);
        return new Rect(
            MathF.Round(Geometry.X + TextPaddingX - _horizontalScroll + width),
            MathF.Round(visualLineBox.Top),
            1,
            Math.Max(1, Math.Min(visualLineBox.Height, Geometry.Bottom - visualLineBox.Top - 1)));
    }

    private Point GetTextOrigin(float fontSize, float lineHeight)
    {
        var naturalLineHeight = MathF.Round(fontSize * TextLayout.DefaultLineHeight);
        var textOffset = (lineHeight - naturalLineHeight) / 2f;
        return new Point(
            Geometry.X + TextPaddingX - _horizontalScroll,
            GetFirstLineTop(lineHeight) + textOffset);
    }

    private float GetFirstLineTop(float lineHeight) => IsMultiline
        ? Geometry.Y + TextPaddingY
        : Geometry.Y + Math.Max(1, (Geometry.Height - lineHeight) / 2f);

    private (float Top, float Height) GetVisualLineBox(float fontSize, float lineHeight, int lineIndex)
    {
        var naturalLineHeight = MathF.Round(fontSize * TextLayout.DefaultLineHeight);
        var visualHeight = Math.Max(lineHeight, naturalLineHeight);
        var lineTop = GetFirstLineTop(lineHeight) + lineIndex * lineHeight;
        return (MathF.Round(lineTop + (lineHeight - visualHeight) / 2f), visualHeight);
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
        var width = MeasureRange(DisplayValue, 0, _caretIndex);
        var viewport = Math.Max(0, Geometry.Width - TextPaddingX * 2 - 2);
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
        var editorFont = ControlDrawing.ResolveFont(this, GetFontSize());
        Rune.DecodeFromUtf16(character, out var rune, out _);
        return ControlDrawing.MeasureRenderedRuneAdvance(rune, editorFont);
    }

    private float MeasureCharacterAdvanceBefore(string text, int end)
    {
        var start = PreviousCodePointIndex(text, end);
        return MeasureCharacterAdvance(text.AsSpan(start, end - start));
    }

    private static Rune DecodeRuneAt(string text, int start)
    {
        Rune.DecodeFromUtf16(text.AsSpan(start), out var rune, out _);
        return rune;
    }

    private static Rune DecodeRuneBefore(string text, int end)
    {
        var start = PreviousCodePointIndex(text, end);
        Rune.DecodeFromUtf16(text.AsSpan(start, end - start), out var rune, out _);
        return rune;
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

    private static (int Start, int End) FindWordAt(string text, int index)
    {
        if (text.Length == 0) return (0, 0);
        index = Math.Clamp(index, 0, text.Length - 1);
        if (!char.IsLetterOrDigit(text[index]) && text[index] != '_') return (index, index + 1);
        var start = index;
        var end = index + 1;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
        return (start, end);
    }

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private void CollapseSelectionOnBlur()
    {
        _selectionAnchor = _caretIndex;
        _isDragging = false;
        _caretOpacity = 0f;
        _caretBlinkAnimation = null;
        InvalidatePaint();
    }

    private readonly record struct LineRange(int Start, int Length)
    {
        internal int End => Start + Length;
    }
}

public class Input : TextEditorBase
{
    protected override bool IsMultiline => false;

    public string Type
    {
        get => GetProperty<string>(nameof(Type)) ?? "text";
        set => SetProperty(nameof(Type), NormalizeType(value));
    }

    protected override string DisplayValue => Type == "password" ? new string('*', Value.Length) : Value;

    public override bool CanCopySelection => Type != "password";

    public override bool CanCutSelection => Type != "password";

    protected override string FilterInput(string text) => Type == "number" ? FilterNumberInput(text) : text;

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(Type) && Type == "number")
            Value = NormalizeNumber(Value);
        if (name == nameof(Value) && Type == "number")
        {
            var normalized = NormalizeNumber(Value);
            if (normalized != Value) Value = normalized;
        }
    }

    public override Size Measure(Size availableSize) => new(200, 36);

    private string FilterNumberInput(string text)
    {
        var current = Value.Remove(SelectionStart, SelectionLength);
        var result = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var candidate = current.Insert(SelectionStart, result.ToString() + ch);
            if (IsNumberCandidate(candidate)) result.Append(ch);
        }
        return result.ToString();
    }

    private static string NormalizeType(string? value)
    {
        value = value?.Trim().ToLowerInvariant();
        return value is "password" or "number" ? value : "text";
    }

    private static string NormalizeNumber(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            var candidate = result.ToString() + ch;
            if (IsNumberCandidate(candidate)) result.Append(ch);
        }
        return result.ToString();
    }

    private static bool IsNumberCandidate(string value)
    {
        if (value.Length == 0 || value == "-" || value == "." || value == "-.") return true;
        return double.TryParse(
            value,
            System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out _);
    }
}

public class TextArea : TextEditorBase
{
    protected override bool IsMultiline => true;
    public override Size Measure(Size availableSize) => new(300, 88);
}
