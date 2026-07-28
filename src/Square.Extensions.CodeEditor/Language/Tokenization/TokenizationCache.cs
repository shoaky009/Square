namespace Square.Extensions.CodeEditor;

/// <summary>按文档缓存行 token 与跨行状态。</summary>
internal sealed class TokenizationCache
{
    private readonly ITokenizer _tokenizer;
    private readonly List<object?> _states = [null];
    private readonly List<IReadOnlyList<TokenSpan>?> _lines = [];
    private int _validUntil;

    public TokenizationCache(ITokenizer tokenizer) => _tokenizer = tokenizer;

    public void InvalidateFromLine(int line)
    {
        _validUntil = Math.Clamp(line, 0, _validUntil);
    }

    public void Reset()
    {
        _states.Clear();
        _states.Add(null);
        _lines.Clear();
        _validUntil = 0;
    }

    public IReadOnlyList<TokenSpan> GetLineTokens(ICodeEditorTextModel model, int line)
    {
        EnsureLine(model, line);
        return _lines[line] ?? [];
    }

    private void EnsureLine(ICodeEditorTextModel model, int line)
    {
        while (_validUntil <= line && _validUntil < model.LineCount)
        {
            var state = _validUntil < _states.Count ? _states[_validUntil] : null;
            var content = model.GetLineContent(_validUntil);
            IReadOnlyList<TokenSpan> tokens;
            if (_tokenizer is IStatefulTokenizer stateful)
            {
                tokens = stateful.TokenizeLine(content, ref state);
            }
            else
            {
                var stringState = state as string ?? "root";
                tokens = _tokenizer.TokenizeLine(content, ref stringState);
                state = stringState;
            }
            while (_lines.Count <= _validUntil) _lines.Add(null);
            _lines[_validUntil] = tokens;
            while (_states.Count <= _validUntil + 1) _states.Add(null);
            _states[_validUntil + 1] = state;
            _validUntil++;
        }
    }
}
