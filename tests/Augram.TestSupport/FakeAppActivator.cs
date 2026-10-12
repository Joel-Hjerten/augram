using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>Fake <see cref="IAppActivator"/>: answers <paramref name="answer"/> to every request and keeps the executables asked for; activates nothing.</summary>
public sealed class FakeAppActivator(AppActivation answer) : IAppActivator
{
    public List<string> Asked { get; } = [];

    public AppActivation BringToFront(string executable)
    {
        Asked.Add(executable);
        return answer;
    }
}
