using Square.Runtime.Binding;

namespace Square.PackageConsumer;

public partial class Main
{
    [Prop]
    public ObservableValue<string> Message { get; } = new("Package consumer");

    public bool CodeBehindLoaded => true;
}
