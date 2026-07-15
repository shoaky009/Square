namespace Square.Runtime.Binding;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class PropAttribute : Attribute
{
    public bool Required { get; set; }
    public object? Default { get; set; }
}