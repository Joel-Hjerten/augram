using Augram.App.Hosting;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class UiThreadTests
{
    [AvaloniaFact]
    public void RunOrPostRunsAtOnceOnTheUiThread_AndPostsFromAnotherThread()
    {
        var ran = new List<string>();

        UiThread.RunOrPost(() => ran.Add("ui"));
        Assert.Equal(["ui"], ran);

        var worker = new Thread(() => UiThread.RunOrPost(() => ran.Add("worker")));
        worker.Start();
        worker.Join();
        Assert.Equal(["ui"], ran);

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["ui", "worker"], ran);
    }
}
