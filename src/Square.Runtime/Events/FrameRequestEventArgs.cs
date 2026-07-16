namespace Square.Events;

public sealed class FrameRequestEventArgs : RoutedEventArgs
{
    public FrameRequestEventArgs(double framesPerSecond = 60d)
    {
        FramesPerSecond = Math.Clamp(framesPerSecond, 1d, 240d);
    }

    public double FramesPerSecond { get; }
    public double IntervalSeconds => 1d / FramesPerSecond;
}
