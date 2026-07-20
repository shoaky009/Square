using System.Globalization;
using System.Text;

namespace Square.Extensions.RichText;

public static class RichTextBoundaries
{
    public static int PreviousTextElement(string text, int offset)
    {
        ValidateOffset(text, offset);
        if (offset == 0) return 0;
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var i = starts.Length - 1; i >= 0; i--)
            if (starts[i] < offset) return starts[i];
        return 0;
    }

    public static int NextTextElement(string text, int offset)
    {
        ValidateOffset(text, offset);
        if (offset == text.Length) return text.Length;
        var starts = StringInfo.ParseCombiningCharacters(text);
        foreach (var start in starts)
            if (start > offset) return start;
        return text.Length;
    }

    public static int PreviousWord(string text, int offset)
    {
        ValidateOffset(text, offset);
        var boundaries = GetElements(text);
        var index = FindElementBefore(boundaries, offset);
        while (index >= 0 && !IsWord(boundaries[index].Value)) index--;
        while (index > 0 && IsWord(boundaries[index - 1].Value)) index--;
        return index >= 0 ? boundaries[index].Start : 0;
    }

    public static int NextWord(string text, int offset)
    {
        ValidateOffset(text, offset);
        var boundaries = GetElements(text);
        var index = FindElementAtOrAfter(boundaries, offset);
        if (index >= boundaries.Count) return text.Length;
        if (IsWord(boundaries[index].Value))
            while (index < boundaries.Count && IsWord(boundaries[index].Value)) index++;
        while (index < boundaries.Count && !IsWord(boundaries[index].Value)) index++;
        return index < boundaries.Count ? boundaries[index].Start : text.Length;
    }

    private static List<TextElement> GetElements(string text)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        var result = new List<TextElement>(starts.Length);
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            result.Add(new TextElement(starts[i], text[starts[i]..end]));
        }
        return result;
    }

    private static int FindElementBefore(IReadOnlyList<TextElement> elements, int offset)
    {
        for (var i = elements.Count - 1; i >= 0; i--)
            if (elements[i].Start < offset) return i;
        return -1;
    }

    private static int FindElementAtOrAfter(IReadOnlyList<TextElement> elements, int offset)
    {
        for (var i = 0; i < elements.Count; i++)
            if (elements[i].Start >= offset) return i;
        return elements.Count;
    }

    private static bool IsWord(string element)
    {
        var rune = element.EnumerateRunes().FirstOrDefault();
        var category = Rune.GetUnicodeCategory(rune);
        return category is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.ConnectorPunctuation;
    }

    private static void ValidateOffset(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset < 0 || offset > text.Length) throw new ArgumentOutOfRangeException(nameof(offset));
    }

    private readonly record struct TextElement(int Start, string Value);
}