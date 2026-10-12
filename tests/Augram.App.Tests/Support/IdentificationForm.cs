using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Xunit;

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

    /// <summary>
    /// Plan 0010 decision 7: the form opens on this machine's platform with its fields editable; the other platform's show
    /// read-only, without a magnifier, under "Edit on Windows" (or macOS).
    /// </summary>
    public static void AssertOnlyThisPlatformEdits(FormScreen screen)
    {
        var here = RunningPlatform.Current;
        var note = Fields(screen).Single(field => field.Label == AppMatcherEditViewModel.ReadOnlyLabel);
        Assert.True(Pattern("Executable", screen).Enabled!.Get());
        Assert.False(note.Visible!.Get());

        On(screen, here.Other());
        Assert.False(Pattern("Executable", screen).Enabled!.Get());
        Assert.False(Pattern("Window title", screen).Enabled!.Get());
        Assert.False(Pattern("Executable", screen).FinderVisible!.Get());
        Assert.True(note.Visible!.Get());
        Assert.Equal($"Edit on {here.Other().DisplayName()}", ((NoteField)note).Text.Get());
    }

    private static IEnumerable<Field> Fields(FormScreen screen) => screen.Sections.SelectMany(section => section.Fields);
}
