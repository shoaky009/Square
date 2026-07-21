using Square.Runtime;
using Xunit;

namespace Square.Runtime.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void RunRaisesExitedAfterNormalExit()
    {
        var application = new TestApplication();
        var exited = false;
        application.Exited += () => exited = true;

        application.Run();

        Assert.True(exited);
        Assert.False(application.IsRunning);
    }

    [Fact]
    public void RunRaisesExitedAfterFailure()
    {
        var application = new TestApplication(new InvalidOperationException("failure"));
        var exited = false;
        application.Exited += () => exited = true;

        Assert.Throws<InvalidOperationException>(application.Run);

        Assert.True(exited);
        Assert.False(application.IsRunning);
    }

    private sealed class TestApplication(Exception? failure = null) : Application
    {
        protected override void RunCore()
        {
            if (failure != null) throw failure;
        }
    }
}
