namespace Square.Extensions.CodeEditor;

/// <summary>括号匹配：在光标旁查找配对括号位置。</summary>
internal static class BracketMatcher
{
    /// <summary>
    /// 返回匹配的两个 offset（开、闭括号各一个字符位置）。
    /// 光标在括号上或紧跟其后时生效。
    /// </summary>
    public static bool TryFindMatch(
        ICodeEditorTextModel model,
        LanguageConfiguration config,
        int caretOffset,
        out int openOffset,
        out int closeOffset)
    {
        openOffset = closeOffset = -1;
        var pairs = config.Brackets ?? [("{", "}"), ("[", "]"), ("(", ")")];
        var openToClose = new Dictionary<char, char>();
        var closeToOpen = new Dictionary<char, char>();
        foreach (var (o, c) in pairs)
        {
            if (o.Length != 1 || c.Length != 1) continue;
            openToClose[o[0]] = c[0];
            closeToOpen[c[0]] = o[0];
        }
        if (openToClose.Count == 0) return false;

        var text = model.GetValue();
        if (text.Length == 0) return false;
        caretOffset = Math.Clamp(caretOffset, 0, text.Length);

        // Prefer bracket under caret-1 (just typed/after), then at caret.
        var candidates = new List<int>(2);
        if (caretOffset > 0) candidates.Add(caretOffset - 1);
        if (caretOffset < text.Length) candidates.Add(caretOffset);

        foreach (var pos in candidates)
        {
            var ch = text[pos];
            if (openToClose.TryGetValue(ch, out var close))
            {
                var match = ScanForward(text, pos, ch, close);
                if (match >= 0)
                {
                    openOffset = pos;
                    closeOffset = match;
                    return true;
                }
            }
            else if (closeToOpen.TryGetValue(ch, out var open))
            {
                var match = ScanBackward(text, pos, open, ch);
                if (match >= 0)
                {
                    openOffset = match;
                    closeOffset = pos;
                    return true;
                }
            }
        }

        return false;
    }

    private static int ScanForward(string text, int from, char open, char close)
    {
        var depth = 0;
        for (var i = from; i < text.Length; i++)
        {
            if (IsInStringOrCommentRough(text, i)) continue;
            var ch = text[i];
            if (ch == open) depth++;
            else if (ch == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int ScanBackward(string text, int from, char open, char close)
    {
        var depth = 0;
        for (var i = from; i >= 0; i--)
        {
            if (IsInStringOrCommentRough(text, i)) continue;
            var ch = text[i];
            if (ch == close) depth++;
            else if (ch == open)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    // Lightweight skip: if current char is inside a simple "..." or '...' on the same line, ignore.
    private static bool IsInStringOrCommentRough(string text, int index)
    {
        // Find line start
        var lineStart = index;
        while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
        var inString = '\0';
        for (var i = lineStart; i < index; i++)
        {
            var ch = text[i];
            if (inString != '\0')
            {
                if (ch == '\\' && i + 1 < index) { i++; continue; }
                if (ch == inString) inString = '\0';
                continue;
            }
            if (ch is '"' or '\'' or '`')
            {
                inString = ch;
                continue;
            }
            // line comment
            if (ch == '/' && i + 1 < text.Length && text[i + 1] == '/')
                return true;
        }
        return inString != '\0';
    }
}
