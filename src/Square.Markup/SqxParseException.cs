namespace Square.Markup;

public sealed class SqxParseException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public SqxParseException(string message, int line, int column)
        : base($"{message} ({line}:{column})")
    {
        Line = line;
        Column = column;
    }
}