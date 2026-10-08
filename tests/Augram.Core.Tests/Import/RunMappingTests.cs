using Augram.Core.Steps.Imported;
using Augram.Core.Steps.Run;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// SP.net <c>Run</c> step parameters and recognised <c>sp.RunProgram</c> calls → <see cref="RunStep"/>. The parameter
/// dictionaries are synthetic in the real shape: <c>MethodParameterReader</c> keeps <c>command</c> as its text.
/// </summary>
public sealed class RunMappingTests
{
    private static Dictionary<string, string> Command(string command) => new(StringComparer.Ordinal) { ["command"] = command };

    [Fact]
    public void TheRealShapesOfARunStepMap()
    {
        Assert.Equal(new RunStep("explorer"), RunMapping.FromRun(Command("explorer")));
        Assert.Equal(new RunStep("ms-settings:display"), RunMapping.FromRun(Command("ms-settings:display")));
        Assert.Equal("Open ms-settings:display", RunMapping.FromRun(Command("ms-settings:display"))!.Summary);
        Assert.Equal(new RunStep(@"C:\Synthetic Tools\tool.exe", "--fast \"a b\""), RunMapping.FromRun(Command(@"""C:\Synthetic Tools\tool.exe"" --fast ""a b""")));
    }

    [Fact]
    public void NoCommandIsNoStep()
    {
        Assert.Null(RunMapping.FromRun(new Dictionary<string, string>(StringComparer.Ordinal)));
        Assert.Null(RunMapping.FromRun(Command("   ")));
        Assert.Null(RunMapping.FromRun(new Dictionary<string, string>(StringComparer.Ordinal) { ["Command"] = "explorer" }));
    }

    [Theory]
    [InlineData("explorer", "explorer", "")]
    [InlineData("  explorer  ", "explorer", "")]
    [InlineData("taskkill.exe /f /im synthetic-emulator.exe", "taskkill.exe", "/f /im synthetic-emulator.exe")]
    [InlineData(@"""C:\Program Files\Synthetic\app.exe"" -x ""y z""", @"C:\Program Files\Synthetic\app.exe", @"-x ""y z""")]
    [InlineData(@"""C:\Program Files\Synthetic\app.exe""", @"C:\Program Files\Synthetic\app.exe", "")]
    [InlineData(@"""C:\never closed\app.exe", @"C:\never closed\app.exe", "")]
    [InlineData(@"C:\Program Files\Synthetic\app.exe -x", @"C:\Program Files\Synthetic\app.exe", "-x")]
    [InlineData(@"C:\Program Files\Synthetic\start.bat", @"C:\Program Files\Synthetic\start.bat", "")]
    [InlineData(@"%WINDIR%\system32\cmd.exe /k echo hi", @"%WINDIR%\system32\cmd.exe", "/k echo hi")]
    [InlineData(@"notepad C:\notes.txt", "notepad", @"C:\notes.txt")]
    [InlineData(@"C:\My Documents\report.pdf", @"C:\My Documents\report.pdf", "")]
    [InlineData(@"\\server\share\Synthetic Folder", @"\\server\share\Synthetic Folder", "")]
    [InlineData("https://example.com/?q=a b", "https://example.com/?q=a b", "")]
    public void ACommandLineSplitsTheWayAPersonReadsIt(string command, string file, string arguments)
    {
        Assert.Equal((file, arguments), RunMapping.SplitCommand(command));
    }

    [Fact]
    public void ARunProgramCallMapsVerbAndStyle()
    {
        Assert.Equal(
            new RunStep("taskkill.exe", "/f /im synthetic-emulator.exe", string.Empty, Elevated: true, Hidden: true),
            RunMapping.FromRunProgram(new RunProgramCall("taskkill.exe", "/f /im synthetic-emulator.exe", "runas", "hidden", true, true, false)));
        Assert.Equal(
            new RunStep(@"J:\Synthetic Drive\Tools\dc64cmd.exe", "-refresh=120"),
            RunMapping.FromRunProgram(new RunProgramCall(@"J:\Synthetic Drive\Tools\dc64cmd.exe", "-refresh=120", "open", "normal", true, false, false)));
        Assert.Equal(
            new RunStep("a.exe", Elevated: true, Hidden: true),
            RunMapping.FromRunProgram(new RunProgramCall("a.exe", string.Empty, " RunAs ", "HIDDEN", false, false, true)));
    }

    [Fact]
    public void NoWindowWaitForExitAndOtherStylesHaveNoEquivalent()
    {
        // noWindow only matters without shell execute, and only for console programs: mapping it to Hidden would hide notepad.
        Assert.Equal(new RunStep(@"C:\Windows\notepad.exe"), RunMapping.FromRunProgram(new RunProgramCall(@"C:\Windows\notepad.exe", string.Empty, string.Empty, string.Empty, false, true, false)));
        Assert.Equal(new RunStep("a.exe"), RunMapping.FromRunProgram(new RunProgramCall("a.exe", string.Empty, "open", "minimized", true, false, true)));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("open", true)]
    [InlineData("Open", true)]
    [InlineData("runas", true)]
    [InlineData("edit", false)]
    [InlineData("print", false)]
    public void OnlyOpenAndRunAsArePlainVerbs(string verb, bool plain)
    {
        Assert.Equal(plain, new RunProgramCall("a.txt", string.Empty, verb, string.Empty, true, false, false).HasPlainVerb);
    }

    [Fact]
    public void ARunPlaceholderUpgradesAndOthersDoNot()
    {
        Assert.Equal(new RunStep("explorer"), RunMapping.TryUpgrade(new ImportedStep("Run", "Open Explorer", Command("explorer"))));
        Assert.Null(RunMapping.TryUpgrade(new ImportedStep("Run", "Empty", ImportedStep.NoParameters)));
        Assert.Null(RunMapping.TryUpgrade(new ImportedStep("SendKeys", "Keys", Command("explorer"))));
    }
}
