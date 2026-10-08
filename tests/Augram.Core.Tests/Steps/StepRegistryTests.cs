using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.TypeText;
using Augram.Core.Steps.WindowOp;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class StepRegistryTests
{
    [Fact]
    public void BuiltInListsTheShippedTypesInPickerOrder()
    {
        string[] expected = ["windowOp", "mediaKey", "hotkey", "typeText", "run", "delay", "imported"];

        Assert.Equal(expected, StepRegistry.BuiltIn.All.Select(type => type.Key).ToArray());
        Assert.Same(WindowOpStepType.Instance, StepRegistry.BuiltIn.Find("windowOp"));
        Assert.Same(MediaKeyStepType.Instance, StepRegistry.BuiltIn.Find("mediaKey"));
        Assert.Same(HotkeyStepType.Instance, StepRegistry.BuiltIn.Find("hotkey"));
        Assert.Same(TypeTextStepType.Instance, StepRegistry.BuiltIn.Find("typeText"));
        Assert.Same(RunStepType.Instance, StepRegistry.BuiltIn.Find("run"));
        Assert.Same(DelayStepType.Instance, StepRegistry.BuiltIn.Find("delay"));
        Assert.Same(ImportedStepType.Instance, StepRegistry.BuiltIn.Find("imported"));
    }

    [Fact]
    public void OnlyThePlaceholderSitsInTheOtherCategory()
    {
        var hidden = StepRegistry.BuiltIn.All.Where(type => type.Category == StepCategory.Other).ToArray();

        var placeholder = Assert.Single(hidden);
        Assert.Equal("imported", placeholder.Key);
    }

    [Fact]
    public void EveryBuiltInTypeRoundTripsItsDefault()
    {
        foreach (var type in StepRegistry.BuiltIn.All)
        {
            var step = type.CreateDefault();
            Assert.Same(type, step.Type);

            var written = type.Write(step);
            var reread = type.Read(written);

            Assert.Equal(step, reread);
            Assert.Equal(written.ToJsonString(), type.Write(reread).ToJsonString());
            Assert.False(string.IsNullOrWhiteSpace(step.Summary));
        }
    }

    [Fact]
    public void FindReturnsNullAndRequireThrowsForUnknownKeys()
    {
        Assert.Null(StepRegistry.BuiltIn.Find("noSuchStep"));
        Assert.Null(StepRegistry.BuiltIn.Find("WindowOp"));

        var ex = Assert.Throws<StepFormatException>(() => StepRegistry.BuiltIn.Require("noSuchStep"));
        Assert.Equal("Unknown step type 'noSuchStep'.", ex.Message);
        Assert.Same(DelayStepType.Instance, StepRegistry.BuiltIn.Require("delay"));
    }

    [Fact]
    public void DuplicateKeysAreRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => new StepRegistry([DelayStepType.Instance, DelayStepType.Instance]));

        Assert.Contains("'delay'", ex.Message, StringComparison.Ordinal);
    }
}
