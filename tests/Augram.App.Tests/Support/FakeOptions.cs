using Augram.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Tests.Support;

/// <summary>A view model with one property per field kind, for binding round-trip tests.</summary>
public sealed partial class FakeOptions : ObservableObject
{
    [ObservableProperty]
    public partial bool Flag { get; set; } = true;

    [ObservableProperty]
    public partial string Choice { get; set; } = "Beta";

    [ObservableProperty]
    public partial string Button { get; set; } = "Right";

    [ObservableProperty]
    public partial string Name { get; set; } = "start";

    [ObservableProperty]
    public partial double Amount { get; set; } = 10;

    [ObservableProperty]
    public partial RgbColor Colour { get; set; } = new(0, 255, 64);
}
