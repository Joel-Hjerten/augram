using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.ClearClipboard;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.DisplayMode;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.Scroll;
using Augram.Core.Steps.TypeText;
using Augram.Core.Steps.WindowOp;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

public sealed class PlaceholderUpgradeTests
{
    private const string ScriptNote = "Imported from StrokesPlus.net: script-only action";

    [Fact]
    public void AMappingWithoutPlaceholdersComesBackAsTheSameInstance()
    {
        var mapping = Mapping(Command("Close", Real(new HotkeyStep(KeyModifiers.Control, KeyCode.W))));

        var result = PlaceholderUpgrade.Upgrade(mapping);

        Assert.Same(mapping, result.Mapping);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void SendKeysBecomesTheStepsItsKeyStringMakes_KeepingPlatformAndActiveFlag()
    {
        var mapping = Mapping(Command("Typed", Placeholder("SendKeys", ("sendKeysString", "^w{DELAY 50}abc"), isActive: false)));

        var result = PlaceholderUpgrade.Upgrade(mapping);

        var steps = Global(result).Commands.Single().Steps;
        Assert.Equal<IStep>(
            [new HotkeyStep(KeyModifiers.Control, KeyCode.W), new DelayStep(50), new TypeTextStep("abc", TypeTextMethod.Unicode)],
            steps.Select(step => step.Step).ToArray());
        Assert.All(steps, step => Assert.Equal((HostPlatform.Windows, false), (step.AuthoredOn, step.IsActive)));
        Assert.Equal(1, result.Methods["SendKeys"]);
    }

    [Fact]
    public void RunAndSendStringBecomeTheirSteps()
    {
        var mapping = Mapping(
            Command("Explorer", Placeholder("Run", ("command", "explorer"))),
            Command("Text", Placeholder("SendString", ("characters", "hello"))));

        var result = PlaceholderUpgrade.Upgrade(mapping);

        Assert.Equal(new RunStep("explorer"), Global(result).Commands[0].Steps.Single().Step);
        Assert.Equal(new TypeTextStep("hello", TypeTextMethod.Unicode), Global(result).Commands[1].Steps.Single().Step);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AOneCallRunProgramScriptBecomesARunStepAndLosesTheScriptNote()
    {
        const string script = "sp.RunProgram(\"taskkill.exe\", \"/f /im synthetic.exe\", \"runas\", \"hidden\", true, true, false);";
        var mapping = Mapping(Command("Kill", Placeholder("Script", ("script", script))) with { Note = ScriptNote });

        var command = Global(PlaceholderUpgrade.Upgrade(mapping)).Commands.Single();

        Assert.Equal(new RunStep("taskkill.exe", "/f /im synthetic.exe", string.Empty, Elevated: true, Hidden: true), command.Steps.Single().Step);
        Assert.Null(command.Note);
    }

    [Fact]
    public void ADisplayChangerScriptBecomesADisplayModeStep_NotARunOfTheProgram()
    {
        const string script = "//Method: RunProgram(...)\r\nsp.RunProgram(\"C:\\\\Tools\\\\Display Changer\\\\dc64cmd.exe\", \"-refresh=100\", \"open\", \"normal\", true, false, false);";
        var mapping = Mapping(Command("Refresh 100", Placeholder("Script", ("script", script))) with { Note = ScriptNote });

        var command = Global(PlaceholderUpgrade.Upgrade(mapping)).Commands.Single();

        Assert.Equal(new DisplayModeStep(Refresh: RefreshRate.FromHertz(100)), command.Steps.Single().Step);
        Assert.Null(command.Note);
    }

    [Fact]
    public void ACommentOnlyScriptIsRemoved_LeavingACommandThatDoesNothingHere()
    {
        const string script = "//Do nothing here: this silences the Global gesture over the desktop\r\n//on purpose";
        var mapping = Mapping(Command("Synthetic Ignore", Placeholder("Script", ("script", script))) with { Note = ScriptNote });

        var result = PlaceholderUpgrade.Upgrade(mapping);

        var command = Global(result).Commands.Single();
        Assert.Empty(command.Steps);
        Assert.Null(command.Note);
        Assert.Equal(1, result.Methods["Script"]);
    }

    [Fact]
    public void SendKeysClipClearSnapAndWheelScriptsBecomeTheirSteps_KeepingTheActiveFlag()
    {
        const string wheel = "var WM_MOUSEWHEEL = 0x20A; var WHEEL_POS = 0x00780000; var WHEEL_NEG = 0xff880000; var MK_CONTROL = 0x08;"
            + " sp.WindowFromPoint(action.Start, false).PostMessage(WM_MOUSEWHEEL, new System.IntPtr(WHEEL_NEG+MK_CONTROL), new System.IntPtr((action.Start.Y << 16) + action.Start.X));";
        const string snap = "var win = action.Window; var screen = win.Screen.WorkingArea; var margin = 0; var halfWidth = Math.floor(screen.Width / 2); win.Restore();"
            + " var rect = win.Rectangle; rect.X = screen.X + margin; rect.Y = screen.Y + margin; rect.Width = halfWidth - margin*1.5; rect.Height = screen.Height - margin*2; win.Rectangle = rect;";
        var mapping = Mapping(
            Command("Zoom", Placeholder("Script", ("script", "// zoom\r\nsp.SendKeys(\"^{ADD}\");"))) with { Note = "Kept line" + Environment.NewLine + ScriptNote },
            Command("Clear", Placeholder("Script", ("script", "clip.Clear();"))),
            Command("Snap", Placeholder("Script", ("script", snap))),
            Command("Wheel", Placeholder("Script", ("script", wheel), isActive: false)));

        var result = PlaceholderUpgrade.Upgrade(mapping);

        var commands = Global(result).Commands;
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.NumPadAdd), commands[0].Steps.Single().Step);
        Assert.Equal("Kept line", commands[0].Note);
        Assert.Equal(new ClearClipboardStep(), commands[1].Steps.Single().Step);
        Assert.Equal(new WindowOpStep(WindowOperation.SnapLeftHalf), commands[2].Steps.Single().Step);
        var scroll = commands[3].Steps.Single();
        Assert.Equal((new ScrollStep(ScrollDirection.Down, 1, KeyModifiers.Control), HostPlatform.Windows, false), (scroll.Step, scroll.AuthoredOn, scroll.IsActive));
        Assert.Equal(4, result.Methods["Script"]);
        Assert.Equal(0, PlaceholderUpgrade.Upgrade(result.Mapping).Count);
    }

    [Fact]
    public void WhatStillDoesNotMapStaysAPlaceholder()
    {
        var click = Placeholder("MouseClick", ("button", "Left"));
        var unknownKey = Placeholder("SendKeys", ("sendKeysString", "{BREAK}"));
        var script = Placeholder("Script", ("script", "sp.MessageBox(\"hi\", \"title\");"));
        var mapping = Mapping(Command("Click", click), Command("Break", unknownKey), Command("Box", script) with { Note = ScriptNote });

        var result = PlaceholderUpgrade.Upgrade(mapping);

        Assert.Same(mapping, result.Mapping);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void OnlyTheChangedCommandIsReplaced_AndASecondRunHasNothingToDo()
    {
        var untouched = Command("Close", Real(new HotkeyStep(KeyModifiers.Control, KeyCode.W)));
        var mapping = Mapping(untouched, Command("Explorer", Real(new DelayStep(10)), Placeholder("Run", ("command", "explorer"))));

        var first = PlaceholderUpgrade.Upgrade(mapping);
        var second = PlaceholderUpgrade.Upgrade(first.Mapping);

        Assert.Same(untouched, Global(first).Commands[0]);
        Assert.Equal<IStep>([new DelayStep(10), new RunStep("explorer")], Global(first).Commands[1].Steps.Select(step => step.Step).ToArray());
        Assert.Same(first.Mapping, second.Mapping);
        Assert.Equal(0, second.Count);
    }

    private static AppGroup Global(PlaceholderUpgradeResult result) => result.Mapping.Groups.Single(group => group.IsGlobal);

    private static MappingDocument Mapping(params Command[] commands) => new([AppGroup.EmptyGlobal with { Commands = commands }], []);

    private static Command Command(string name, params CommandStep[] steps) => new(CommandId.New(), name, Trigger.None, IsActive: true, steps);

    private static CommandStep Real(IStep step) => new(step, HostPlatform.Windows);

    private static CommandStep Placeholder(string method, (string Name, string Value) parameter, bool isActive = true)
        => new(new ImportedStep(method, method, new Dictionary<string, string> { [parameter.Name] = parameter.Value }), HostPlatform.Windows, isActive);
}
