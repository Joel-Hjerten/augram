using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.Imported;
using Augram.App.Components.Steps.WindowOp;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class StepFormRegistryTests
{
    /// <summary>ADR-0002 §4: every shipped step type has a form under Components/Steps/&lt;Name&gt;/, found by scan.</summary>
    [Fact]
    public void EveryBuiltInStepTypeHasAForm()
    {
        foreach (var type in StepRegistry.BuiltIn.All)
        {
            Assert.True(StepFormRegistry.Default.Supports(type), $"No IStepForm for step type '{type.Key}'.");
        }

        Assert.Equal(StepRegistry.BuiltIn.All.Count, StepFormRegistry.Default.TypeKeys.Count);
    }

    [AvaloniaFact]
    public void WindowOpFormShowsSizeFieldsOnlyForSetSizeAndDefaultsTo1280By720()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new WindowOpStep(WindowOperation.Minimize), changes.Add);
        Assert.Single(form.GetVisualDescendants().OfType<FieldRow>());
        var operation = form.GetVisualDescendants().OfType<ComboBox>().Single();
        Assert.Equal("Minimize window", operation.SelectedItem);

        operation.SelectedIndex = (int)WindowOperation.SetSize;

        Assert.Equal(new WindowOpStep(WindowOperation.SetSize, new WindowSize(WindowOpStepForm.DefaultWidth, WindowOpStepForm.DefaultHeight)), Assert.Single(changes));
        form.UpdateLayout();
        var numbers = form.GetVisualDescendants().OfType<NumericUpDown>().ToList();
        Assert.Equal(2, numbers.Count);
        Assert.Equal((1280m, 720m), (numbers[0].Value, numbers[1].Value));

        numbers[0].Value = 800;

        Assert.Equal(new WindowOpStep(WindowOperation.SetSize, new WindowSize(800, 720)), changes[^1]);

        form.GetVisualDescendants().OfType<ComboBox>().Single().SelectedIndex = (int)WindowOperation.Close;

        Assert.Equal(new WindowOpStep(WindowOperation.Close), changes[^1]);
        form.UpdateLayout();
        Assert.Empty(form.GetVisualDescendants().OfType<NumericUpDown>());
    }

    [AvaloniaFact]
    public void DelayAndMediaKeyFormsRaiseChangedWithTheEditedRecord()
    {
        var changes = new List<IStep>();
        var (delay, _) = Show(new DelayStep(30), changes.Add);
        delay.GetVisualDescendants().OfType<NumericUpDown>().Single().Value = 250;
        Assert.Equal(new DelayStep(250), Assert.Single(changes));

        changes.Clear();
        var (media, _) = Show(new MediaKeyStep(MediaKeyKind.VolumeUp), changes.Add);
        var key = media.GetVisualDescendants().OfType<ComboBox>().Single();
        Assert.Equal("Volume up", key.SelectedItem);
        key.SelectedIndex = (int)MediaKeyKind.PlayPause;
        Assert.Equal(new MediaKeyStep(MediaKeyKind.PlayPause), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void ImportedFormIsReadOnlyAndSaysWhyTheStepDoesNothing()
    {
        var changes = new List<IStep>();
        var step = new ImportedStep("SendKeys", "Send Ctrl+W", new Dictionary<string, string> { ["Keys"] = "^w", ["Delay"] = "0" });
        var (form, _) = Show(step, changes.Add);

        var texts = form.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();

        Assert.Contains("SendKeys", texts);
        Assert.Contains("Send Ctrl+W", texts);
        Assert.Contains(texts, text => text is not null && text.Contains("Keys: ^w", StringComparison.Ordinal) && text.Contains("Delay: 0", StringComparison.Ordinal));
        Assert.Contains(ImportedStepForm.NotSupportedText, texts);
        Assert.Empty(form.GetVisualDescendants().OfType<TextBox>());
        Assert.Empty(changes);
    }

    /// <summary>Opened with This app ticked, unticking it brings the app rows back (Joel, 2026-10-09: they stayed hidden).</summary>
    [AvaloniaFact]
    public void OpenAppFormOpenedOnThisApp_ShowsTheAppRowsWhenItIsUnticked()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new Augram.Core.Steps.OpenApp.OpenAppStep(true, "chrome.exe", string.Empty), changes.Add);
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();
        var app = rows.Single(row => row.Label == "Windows app");
        Assert.False(app.IsVisible);

        ((CheckBox)rows.Single(row => row.Label == Augram.App.Components.Steps.OpenApp.OpenAppStepForm.AugramLabel).Editor!).IsChecked = false;

        Assert.True(app.IsVisible);
        Assert.False(Assert.IsType<Augram.Core.Steps.OpenApp.OpenAppStep>(changes[^1]).IsAugram);
        Assert.Equal("chrome.exe", ((TextBox)app.Editor!).Text);
    }

    private static (Control Form, Window Window) Show(IStep step, Action<IStep> changed)
    {
        var form = StepFormRegistry.Default.Build(step, changed);
        var window = new Window { Content = form, Width = 600, Height = 400 };
        window.Show();
        return (form, window);
    }
}
