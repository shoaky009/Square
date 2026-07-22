using Square.PackageConsumer;

var component = new Main();
component.BuildElementTree();

if (component.Children.Count != 1)
    throw new InvalidOperationException("The packaged source generator did not build the SQX component.");
