using Augram.Core.Abstractions;
using Augram.Platform.Windows.Startup;
using Xunit;

namespace Augram.Platform.Windows.Tests.Startup;

/// <summary>The Run value and Task Manager's switch, over a scripted registry; no test writes the real Run key.</summary>
public sealed class RunKeyStartupRegistrationTests
{
    private const string Name = "Augram";
    private const string Command = "\"C:\\Users\\joel\\AppData\\Local\\Augram\\current\\Augram.App.exe\" --hidden";
    private static readonly byte[] Disabled = [0x03, 0, 0, 0, 0x5A, 0x1F, 0x3C, 0x22, 0x9E, 0x40, 0xDB, 0x01];
    private static readonly byte[] Enabled = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    [Fact]
    public void TheCommandIsTheQuotedPathAndTheArguments()
    {
        Assert.Equal("\"C:\\Program Files\\Augram\\Augram.App.exe\" --hidden", RunKeyStartupRegistration.CommandFor(@"C:\Program Files\Augram\Augram.App.exe", "--hidden"));
        Assert.Equal("\"C:\\x\\Augram.App.exe\"", RunKeyStartupRegistration.CommandFor(@"C:\x\Augram.App.exe", null));
        Assert.Equal($"\"{Environment.ProcessPath}\" --hidden", new RunKeyStartupRegistration("--hidden").Command);
        Assert.Equal(RunKeyStartupRegistration.DefaultValueName, new RunKeyStartupRegistration("--hidden").ValueName);
    }

    [Fact]
    public void Fresh_IsNotRegistered_AndSetWritesTheCommand()
    {
        var registry = new FakeStartupRegistry();
        var registration = new RunKeyStartupRegistration(registry, Name, Command);

        Assert.Equal(StartupStatus.NotRegistered, registration.Status);

        registration.Set(true);

        Assert.Equal(Command, registry.Commands[Name]);
        Assert.Equal(StartupStatus.Registered, registration.Status);
        Assert.Equal(Command, registration.RegisteredCommand);
    }

    [Theory]
    [InlineData("\"C:\\Users\\joel\\AppData\\Local\\Augram\\current\\Augram.App.exe\"")]
    [InlineData("\"D:\\old\\Augram.App.exe\" --hidden")]
    [InlineData("C:\\Users\\joel\\AppData\\Local\\Augram\\current\\Augram.App.exe --hidden")]
    public void AnotherCommand_IsOutdated_AndSetRewritesIt(string stale)
    {
        // The 0.5–0.7 builds wrote the bare quoted path, with no --hidden; a moved executable leaves another path.
        var registry = new FakeStartupRegistry();
        registry.Commands[Name] = stale;
        var registration = new RunKeyStartupRegistration(registry, Name, Command);

        Assert.Equal(StartupStatus.Outdated, registration.Status);

        registration.Set(true);

        Assert.Equal(Command, registry.Commands[Name]);
        Assert.Equal(StartupStatus.Registered, registration.Status);
    }

    [Fact]
    public void TheSameCommandInAnotherCase_IsRegistered()
    {
        var registry = new FakeStartupRegistry();
        registry.Commands[Name] = Command.ToUpperInvariant();

        Assert.Equal(StartupStatus.Registered, new RunKeyStartupRegistration(registry, Name, Command).Status);
    }

    [Fact]
    public void SwitchedOffInTaskManager_IsDisabledByTheUser_EvenWhenTheCommandIsStale()
    {
        var registry = new FakeStartupRegistry();
        registry.Commands[Name] = Command;
        registry.Approvals[Name] = Disabled;
        var registration = new RunKeyStartupRegistration(registry, Name, Command);

        Assert.Equal(StartupStatus.DisabledByUser, registration.Status);

        registry.Commands[Name] = "\"D:\\old\\Augram.App.exe\"";
        Assert.Equal(StartupStatus.DisabledByUser, registration.Status);
    }

    [Fact]
    public void SwitchedBackOnInTaskManager_IsRegistered()
    {
        var registry = new FakeStartupRegistry();
        registry.Commands[Name] = Command;
        registry.Approvals[Name] = Enabled;

        Assert.Equal(StartupStatus.Registered, new RunKeyStartupRegistration(registry, Name, Command).Status);
    }

    [Fact]
    public void ATaskManagerRecordWithNoRunValue_IsNotRegistered()
    {
        var registry = new FakeStartupRegistry();
        registry.Approvals[Name] = Disabled;

        Assert.Equal(StartupStatus.NotRegistered, new RunKeyStartupRegistration(registry, Name, Command).Status);
    }

    [Fact]
    public void TurningItOn_ClearsTaskManagersSwitchOff()
    {
        var registry = new FakeStartupRegistry();
        registry.Commands[Name] = Command;
        registry.Approvals[Name] = Disabled;
        var registration = new RunKeyStartupRegistration(registry, Name, Command);

        registration.Set(true);

        Assert.False(registry.Approvals.ContainsKey(Name));
        Assert.Equal(StartupStatus.Registered, registration.Status);
        Assert.Equal([$"write {Name}", $"delete approval {Name}"], registry.Calls);
    }

    [Fact]
    public void TurningItOff_RemovesBothValues_AndTwiceIsHarmless()
    {
        var registry = new FakeStartupRegistry();
        registry.Commands[Name] = Command;
        registry.Approvals[Name] = Disabled;
        var registration = new RunKeyStartupRegistration(registry, Name, Command);

        registration.Set(false);
        registration.Set(false);

        Assert.Empty(registry.Commands);
        Assert.Empty(registry.Approvals);
        Assert.Equal(StartupStatus.NotRegistered, registration.Status);
        Assert.Null(registration.RegisteredCommand);
    }

    [Fact]
    public void AnotherValueIsLeftAlone()
    {
        var registry = new FakeStartupRegistry();
        registry.Commands["OneDrive"] = "\"C:\\OneDrive.exe\" /background";
        registry.Approvals["OneDrive"] = Disabled;
        var registration = new RunKeyStartupRegistration(registry, Name, Command);

        registration.Set(true);
        registration.Set(false);

        Assert.Equal("\"C:\\OneDrive.exe\" /background", registry.Commands["OneDrive"]);
        Assert.Equal(Disabled, registry.Approvals["OneDrive"]);
    }
}
