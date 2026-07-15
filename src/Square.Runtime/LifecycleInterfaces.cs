namespace Square.Runtime;

public interface IComponentLifecycle
{
    void OnPropChanged(string name);
    void OnAttached();
    void OnDetached();
    void OnLoaded();
    void OnUnloaded();
}

public interface ILayoutLifecycle
{
    void OnMeasure();
    void OnArrange();
}