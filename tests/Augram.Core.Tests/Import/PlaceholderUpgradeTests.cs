using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.TypeText;
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
