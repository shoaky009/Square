namespace Square.Backends.Impeller;

public sealed class ImpellerException : InvalidOperationException
{
    public ImpellerException(string message) : base(message) { }
    public ImpellerException(string message, Exception innerException) : base(message, innerException) { }
}
