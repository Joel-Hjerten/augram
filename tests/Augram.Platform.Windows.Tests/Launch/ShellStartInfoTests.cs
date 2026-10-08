using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Launch;
using Xunit;

namespace Augram.Platform.Windows.Tests.Launch;

/// <summary>What the Windows launcher asks ShellExecuteEx for; only the start info is built, nothing is started.</summary>
public sealed class ShellStartInfoTests
{
    private const string Home = @"C:\Users\synthetic";

    private static string Expand(string text) => text.Replace("%WINDIR%", @"C:\Windows", StringComparison.OrdinalIgnoreCase).Replace("%TEMP%", @"C:\Temp", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void APlainProgramIsShellExecutedInTheHomeFolder()
    {
        var info = ShellStartInfo.For(new ProcessLaunch("explorer"), Expand, Home);

        Assert.Equal("explorer", info.FileName);
        Assert.Equal(string.Empty, info.Arguments);
        Assert.Equal(Home, info.WorkingDirectory);
        Assert.True(info.UseShellExecute);
        Assert.Equal(string.Empty, info.Verb);
        Assert.Equal(ProcessWindowStyle.Normal, info.WindowStyle);
        Assert.False(info.ErrorDialog);
    }

    [Fact]
    public void ElevatedIsRunAsAndHiddenIsAHiddenWindow()
    {
        var info = ShellStartInfo.For(new ProcessLaunch("taskkill.exe", "/f /im synthetic-emulator.exe", Elevated: true, Hidden: true), Expand, Home);

        Assert.Equal("taskkill.exe", info.FileName);
        Assert.Equal("/f /im synthetic-emulator.exe", info.Arguments);
        Assert.Equal("runas", info.Verb);
        Assert.Equal(ProcessWindowStyle.Hidden, info.WindowStyle);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void VariablesExpandInTheFileAndStartInButNeverInTheArgumentsOrALink()
    {
        var program = ShellStartInfo.For(new ProcessLaunch(@"%WINDIR%\notepad.exe", "%TEMP%\\notes.txt", "%TEMP%"), Expand, Home);
        Assert.Equal(@"C:\Windows\notepad.exe", program.FileName);
        Assert.Equal(@"C:\Temp", program.WorkingDirectory);
        Assert.Equal(@"%TEMP%\notes.txt", program.Arguments);

        var link = ShellStartInfo.For(new ProcessLaunch("https://example.com/%TEMP%"), Expand, Home);
        Assert.Equal("https://example.com/%TEMP%", link.FileName);
    }

    [Fact]
    public void TheRealExpansionKnowsWindir()
    {
        var info = ShellStartInfo.For(new ProcessLaunch(@"%WINDIR%\explorer.exe"), Environment.ExpandEnvironmentVariables, Home);

        Assert.DoesNotContain('%', info.FileName);
        Assert.EndsWith(@"\explorer.exe", info.FileName, StringComparison.OrdinalIgnoreCase);
    }
}
