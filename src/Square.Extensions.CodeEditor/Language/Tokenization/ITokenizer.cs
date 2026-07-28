namespace Square.Extensions.CodeEditor;

/// <summary>行内 token。</summary>
public readonly record struct TokenSpan(int Start, int Length, string Type);

/// <summary>分词器。</summary>
public interface ITokenizer
{
    /// <summary>对单行分词；state 为跨行状态（状态名）。</summary>
    IReadOnlyList<TokenSpan> TokenizeLine(string line, ref string state);
}

/// <summary>支持对象形式跨行状态的分词器，供复杂 grammar rule stack 使用。</summary>
internal interface IStatefulTokenizer
{
    IReadOnlyList<TokenSpan> TokenizeLine(string line, ref object? state);
}

/// <summary>整行单一 token。</summary>
public sealed class PlainTextTokenizer : ITokenizer
{
    /// <inheritdoc/>
    public IReadOnlyList<TokenSpan> TokenizeLine(string line, ref string state)
    {
        state = "root";
        if (line.Length == 0) return [];
        return [new TokenSpan(0, line.Length, "source")];
    }
}
