using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Inspector;

public sealed class InspectorReportTests
{
    [AvaloniaFact]
    public void DescribesARegionOneLinePerItem()
    {
        var control = new Border();
        Region.Mark(control, "Stroke button", new RegionInfo("Options › General › Stroke button", "ButtonRadio", new SourceLocation(@"C:\x\OptionsScreen.cs", 17), "StrokeButton"));

        var text = InspectorReport.Describe(control, "Wireframe");

        Assert.Equal(
            [
                "path: Options › General › Stroke button",
                "component: Border \"Stroke button\"",
                "kind: ButtonRadio",
                @"source: C:\x\OptionsScreen.cs:17",
                "binding: StrokeButton",
                "theme: Wireframe",
            ],
            text.Split(Environment.NewLine));
    }

    [AvaloniaFact]
    public void FallsBackForStructuralRegions()
    {
        var control = new Border();
        Region.Mark(control, "Diagnostics");

        var text = InspectorReport.Describe(control, "Wireframe");

        Assert.Contains("path: Diagnostics", text, StringComparison.Ordinal);
        Assert.Contains("binding: (none)", text, StringComparison.Ordinal);
    }
}
