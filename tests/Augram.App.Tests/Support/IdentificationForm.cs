using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;

namespace Augram.App.Tests.Support;

/// <summary>
/// The app identification form as tests drive it: set its Windows | macOS switch (it opens on the machine's own platform,
/// so a test that runs on both CI runners sets it first) and find a pattern row by label.
/// </summary>
internal static class IdentificationForm
{
    public static void On(FormScreen screen, HostPlatform platform)
        => ((ButtonRadioField<HostPlatform>)Fields(screen).Single(field => field.Label == AppMatcherEditViewModel.PlatformSwitchLabel)).Value.Set(platform);

    public static PatternField Pattern(string label, FormScreen screen) => (PatternField)Fields(screen).Single(field => field.Label == label);

    private static IEnumerable<Field> Fields(FormScreen screen) => screen.Sections.SelectMany(section => section.Fields);
}
