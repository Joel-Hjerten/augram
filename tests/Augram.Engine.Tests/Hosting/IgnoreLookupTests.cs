using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// One pass of the ignore-list watch, without its thread: lookups deduplicated by window, a focus change re-checking a
/// motionless pointer, the bounded rate without cheap keys, and nothing looked up while nothing is watched.
/// </summary>
public sealed class IgnoreLookupTests
{
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
    private static readonly WindowIdentity BlenderWindow = FakeWindowSystem.Identity("blender.exe", handle: 0x50);
    private static readonly WindowIdentity NotepadWindow = FakeWindowSystem.Identity("notepad.exe", handle: 0x10);
    private static readonly WindowIdentity VMwareWindow = FakeWindowSystem.Identity("vmware.exe", handle: 0x60);

    private readonly FakeWindowSystem _windows = new();
    private readonly FakeClock _clock = new();

    private static MappingDocument Mapping(params IgnoredApp[] ignored) => new([AppGroup.EmptyGlobal], ignored);

    private static long At(int x, int y) => IgnoreLookup.Pack(x, y);

    [Fact]
    public void Pack_RoundTripsNegativeCoordinates()
        => Assert.Equal((-2560, -213), IgnoreLookup.Unpack(IgnoreLookup.Pack(-2560, -213)));

    [Fact]
    public void MovesInsideOneWindow_LookItUpOnce()
    {
        var lookup = NewLookup();
        _windows.Window = NotepadWindow;
        var mapping = Mapping(Blender);

        lookup.Pass(mapping, At(10, 10), forget: false);
        lookup.Pass(mapping, At(20, 10), forget: false);
        lookup.Pass(mapping, At(30, 10), forget: false);

        Assert.Single(_windows.Lookups);
        Assert.Equal(3, _windows.KeyLookups);
        Assert.Null(lookup.Over);
    }

    [Fact]
    public void ThePointerStillAtTheSamePlace_AsksNothingAgain()
    {
        var lookup = NewLookup();
        var mapping = Mapping(Blender);
        _windows.Window = BlenderWindow;

        lookup.Pass(mapping, At(10, 10), forget: false);
        lookup.Pass(mapping, At(10, 10), forget: false);

        Assert.Equal(1, _windows.KeyLookups);
        Assert.Same(Blender, lookup.Over);
    }

    [Fact]
    public void AnotherWindow_IsLookedUpAndJudged()
    {
        var lookup = NewLookup();
        var mapping = Mapping(Blender);
        _windows.Window = BlenderWindow;
        lookup.Pass(mapping, At(10, 10), forget: false);
        Assert.Same(Blender, lookup.Over);

        _windows.Window = NotepadWindow;
        lookup.Pass(mapping, At(500, 10), forget: false);

        Assert.Null(lookup.Over);
        Assert.Equal(2, _windows.Lookups.Count);
    }

    [Fact]
    public void NoWindowUnderThePointer_IsNotLookedUp()
    {
        var lookup = NewLookup();
        _windows.Window = null;

        lookup.Pass(Mapping(Blender), At(10, 10), forget: false);

        Assert.Empty(_windows.Lookups);
        Assert.Null(lookup.Over);
    }

    [Fact]
    public void AFocusChange_RechecksAMotionlessPointer()
    {
        var lookup = NewLookup();
        var mapping = Mapping(Blender);
        _windows.Window = NotepadWindow;
        _windows.ForegroundWindow = NotepadWindow;
        lookup.Pass(mapping, At(10, 10), forget: false);
        Assert.Null(lookup.Over);

        // Blender opened under the pointer and took focus; the pointer did not move.
        _windows.Window = BlenderWindow;
        _windows.ForegroundWindow = BlenderWindow;
        lookup.Pass(mapping, At(10, 10), forget: false);

        Assert.Same(Blender, lookup.Over);
    }

    [Fact]
    public void WithoutAFocusChange_AMotionlessPointerIsNotRechecked()
    {
        // The documented staleness window: a window that appears under a still pointer without taking focus is seen on the next move.
        var lookup = NewLookup();
        var mapping = Mapping(Blender);
        _windows.Window = NotepadWindow;
        lookup.Pass(mapping, At(10, 10), forget: false);

        _windows.Window = BlenderWindow;
        lookup.Pass(mapping, At(10, 10), forget: false);
        Assert.Null(lookup.Over);

        lookup.Pass(mapping, At(11, 10), forget: false);
        Assert.Same(Blender, lookup.Over);
    }

    [Fact]
    public void Forget_LooksEverythingUpAgain()
    {
        var lookup = NewLookup();
        _windows.Window = BlenderWindow;
        lookup.Pass(Mapping(Blender), At(10, 10), forget: false);

        lookup.Pass(Mapping(Blender), At(10, 10), forget: true);

        Assert.Equal(2, _windows.Lookups.Count);
        Assert.Same(Blender, lookup.Over);
    }

    [Fact]
    public void NothingWatched_LooksNothingUp()
    {
        var lookup = NewLookup();
        _windows.Window = BlenderWindow;
        var inactive = Blender with { IsActive = false };

        lookup.Pass(Mapping(inactive), At(10, 10), forget: false);
        lookup.Pass(Mapping(), At(20, 10), forget: false);

        Assert.False(lookup.WatchesPointer);
        Assert.Equal(0, _windows.KeyLookups);
        Assert.Equal(0, _windows.ForegroundKeyLookups);
        Assert.Null(lookup.Over);
    }

    [Fact]
    public void AMappingChange_RejudgesTheSameWindowWithoutALookup()
    {
        var lookup = NewLookup();
        _windows.Window = BlenderWindow;
        lookup.Pass(Mapping(Blender), At(10, 10), forget: false);
        Assert.Same(Blender, lookup.Over);

        var renamed = Blender with { Name = "Blender 4" };
        lookup.Pass(Mapping(renamed, VMware), At(10, 10), forget: false);

        Assert.Same(renamed, lookup.Over);
        Assert.Single(_windows.Lookups);
    }

    [Fact]
    public void TheFocusedWindowIsRead_OnlyWhileAPausingAppIsActive()
    {
        var lookup = NewLookup();
        _windows.ForegroundWindow = VMwareWindow;

        lookup.Pass(Mapping(Blender), IgnoreLookup.NoPointer, forget: false);
        Assert.Equal(0, _windows.ForegroundLookups);
        Assert.Null(lookup.PausedBy);
        Assert.False(lookup.WatchesFocus);

        var pausing = Mapping(Blender, VMware);
        lookup.Pass(pausing, IgnoreLookup.NoPointer, forget: false);
        Assert.Equal(1, _windows.ForegroundLookups);
        Assert.Same(VMware, lookup.PausedBy);

        // Focus did not move: the identity is not read again.
        lookup.Pass(pausing, IgnoreLookup.NoPointer, forget: false);
        Assert.Equal(1, _windows.ForegroundLookups);

        _windows.ForegroundWindow = NotepadWindow;
        lookup.Pass(pausing, IgnoreLookup.NoPointer, forget: false);
        Assert.Null(lookup.PausedBy);
    }

    [Fact]
    public void APassNotDueForFocus_DoesNotAskForIt_UnlessTheMappingChangedOrKeysWereDropped()
    {
        var lookup = NewLookup();
        var mapping = Mapping(Blender);

        lookup.Pass(mapping, At(10, 10), forget: false, checkFocus: false);
        Assert.True(lookup.FocusChecked);
        Assert.Equal(1, _windows.ForegroundKeyLookups);

        lookup.Pass(mapping, At(20, 10), forget: false, checkFocus: false);
        Assert.False(lookup.FocusChecked);
        Assert.Equal(1, _windows.ForegroundKeyLookups);

        lookup.Pass(mapping, At(20, 10), forget: true, checkFocus: false);
        lookup.Pass(Mapping(Blender, VMware), At(20, 10), forget: false, checkFocus: false);
        Assert.Equal(3, _windows.ForegroundKeyLookups);
    }

    [Fact]
    public void WithoutCheapKeys_ThePointerIsLookedUpAtMostEveryInterval_AndTheLatestPositionIsRetried()
    {
        var lookup = NewLookup();
        var mapping = Mapping(Blender);
        _windows.CheapKeys = false;
        _windows.Window = NotepadWindow;

        lookup.Pass(mapping, At(10, 10), forget: false);
        Assert.Single(_windows.Lookups);

        _windows.Window = BlenderWindow;
        _clock.MonotonicMs = 40;
        lookup.Pass(mapping, At(20, 10), forget: false);
        Assert.Single(_windows.Lookups);
        Assert.Equal(100, lookup.RetryAtMs);
        Assert.Null(lookup.Over);

        _clock.MonotonicMs = 100;
        lookup.Pass(mapping, At(20, 10), forget: false);
        Assert.Equal(new[] { (10, 10), (20, 10) }, _windows.Lookups);
        Assert.Null(lookup.RetryAtMs);
        Assert.Same(Blender, lookup.Over);
    }

    private IgnoreLookup NewLookup() => new(_windows, HostPlatform.Windows, _clock, TimeSpan.FromMilliseconds(100));
}
