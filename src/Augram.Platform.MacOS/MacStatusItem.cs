using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS;

/// <summary>One entry of the menu <see cref="MacStatusItem"/> shows: a separator, or a title with its check mark, enabled state and action.</summary>
public sealed record MacMenuEntry(string Title, bool IsChecked, bool IsEnabled, Action? Invoke)
{
    public static MacMenuEntry Separator { get; } = new(string.Empty, false, false, null);

    public bool IsSeparator => ReferenceEquals(this, Separator);
}

/// <summary>
/// Augram's own menu-bar item (Joel, 2026-10-09: a click toggles, a double click opens the window, as on Windows). Avalonia's
/// tray item opens its menu on every click and never reports the click, so this is a native <c>NSStatusItem</c> whose button
/// sends its action on a left or right mouse-up: a left click raises <see cref="Clicked"/> (once per click; the caller tells
/// single from double, as on Windows), a right click or a Control-click shows the menu <see cref="MenuEntries"/> gives at that
/// moment, built fresh each time so it always shows the current state. Main thread only (AppKit); the UI thread is the main
/// thread on macOS.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed unsafe class MacStatusItem : IDisposable
{
    private const double VariableLength = -1;
    private const nuint LeftMouseUpMask = 1 << 2;
    private const nuint RightMouseUpMask = 1 << 4;
    private const nuint LeftMouseUpType = 2;
    private const nuint RightMouseUpType = 4;
    private const nuint ControlKeyFlag = 1 << 18;
    private const nint OnState = 1;
    private const nint OffState = 0;

    /// <summary>The menu-bar image's size in points; the PNGs are 36 px, so they draw at 2x on a Retina display.</summary>
    private const double ImagePoints = 18;

    private static readonly ConcurrentDictionary<nint, MacStatusItem> ByTarget = new();
    private static readonly Lazy<nint> TargetClass = new(RegisterTargetClass);

    private readonly nint _item;
    private readonly nint _button;
    private readonly nint _target;
    private IReadOnlyList<MacMenuEntry> _shownEntries = [];
    private nint _image;
    private bool _disposed;

    public MacStatusItem()
    {
        var bar = MacNative.SendPtr(ObjC.Class("NSStatusBar"), ObjC.Selector("systemStatusBar"));
        _item = MacNative.SendPtr(MacNative.SendPtr(bar, ObjC.Selector("statusItemWithLength:"), VariableLength), ObjC.Selector("retain"));
        _button = MacNative.SendPtr(_item, ObjC.Selector("button"));
        _target = MacNative.SendPtr(MacNative.SendPtr(TargetClass.Value, ObjC.Selector("alloc")), ObjC.Selector("init"));
        ByTarget[_target] = this;
        MacNative.SendVoid(_button, ObjC.Selector("setTarget:"), _target);
        MacNative.SendVoid(_button, ObjC.Selector("setAction:"), ObjC.Selector("augramClicked:"));
        MacNative.SendPtr(_button, ObjC.Selector("sendActionOn:"), LeftMouseUpMask | RightMouseUpMask);
    }

    /// <summary>A left click on the item (not a Control-click); raised once per click, the second of a double click included.</summary>
    public event EventHandler? Clicked;

    /// <summary>The menu to show on a right click; asked at that moment.</summary>
    public Func<IReadOnlyList<MacMenuEntry>>? MenuEntries { get; set; }

    /// <summary>Shows <paramref name="png"/> in the menu bar; a template image is tinted by the system for light and dark menu bars.</summary>
    public void SetImage(byte[] png, bool isTemplate)
    {
        ArgumentNullException.ThrowIfNull(png);
        nint image;
        fixed (byte* bytes = png)
        {
            var data = MacNative.SendPtr(ObjC.Class("NSData"), ObjC.Selector("dataWithBytes:length:"), (nint)bytes, (nuint)png.Length);
            image = MacNative.SendPtr(MacNative.SendPtr(ObjC.Class("NSImage"), ObjC.Selector("alloc")), ObjC.Selector("initWithData:"), data);
        }

        if (image == 0)
        {
            return;
        }

        MacNative.SendVoid(image, ObjC.Selector("setSize:"), new MacNative.CGSize { Width = ImagePoints, Height = ImagePoints });
        MacNative.SendVoid(image, ObjC.Selector("setTemplate:"), isTemplate ? (byte)1 : (byte)0);
        MacNative.SendVoid(_button, ObjC.Selector("setImage:"), image);
        if (_image != 0)
        {
            MacNative.SendVoid(_image, ObjC.Selector("release"));
        }

        _image = image;
    }

    public void SetToolTip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var value = Cf.String(text);
        try
        {
            MacNative.SendVoid(_button, ObjC.Selector("setToolTip:"), value);
        }
        finally
        {
            Cf.Release(value);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ByTarget.TryRemove(_target, out _);
        var bar = MacNative.SendPtr(ObjC.Class("NSStatusBar"), ObjC.Selector("systemStatusBar"));
        MacNative.SendVoid(bar, ObjC.Selector("removeStatusItem:"), _item);
        MacNative.SendVoid(_item, ObjC.Selector("release"));
        MacNative.SendVoid(_target, ObjC.Selector("release"));
        if (_image != 0)
        {
            MacNative.SendVoid(_image, ObjC.Selector("release"));
        }
    }

    /// <summary>A small NSObject subclass whose two methods call back here: the button's action and every menu item's.</summary>
    private static nint RegisterTargetClass()
    {
        var cls = MacNative.objc_allocateClassPair(ObjC.Class("NSObject"), "AugramStatusItemTarget", 0);
        MacNative.class_addMethod(cls, ObjC.Selector("augramClicked:"), &OnButton, "v@:@");
        MacNative.class_addMethod(cls, ObjC.Selector("augramMenuItem:"), &OnMenuItem, "v@:@");
        MacNative.objc_registerClassPair(cls);
        return cls;
    }

    [UnmanagedCallersOnly]
    private static void OnButton(nint self, nint selector, nint sender)
    {
        if (!ByTarget.TryGetValue(self, out var item))
        {
            return;
        }

        try
        {
            item.ButtonClicked();
        }
        catch (Exception)
        {
            // An exception must not unwind into AppKit; the click is lost, the app keeps running.
        }
    }

    [UnmanagedCallersOnly]
    private static void OnMenuItem(nint self, nint selector, nint sender)
    {
        if (!ByTarget.TryGetValue(self, out var item))
        {
            return;
        }

        try
        {
            var index = (int)MacNative.SendNInt(sender, ObjC.Selector("tag"));
            var entries = item._shownEntries;
            if (index >= 0 && index < entries.Count)
            {
                entries[index].Invoke?.Invoke();
            }
        }
        catch (Exception)
        {
            // As above: never into AppKit.
        }
    }

    private void ButtonClicked()
    {
        var app = MacNative.SendPtr(ObjC.Class("NSApplication"), ObjC.Selector("sharedApplication"));
        var current = MacNative.SendPtr(app, ObjC.Selector("currentEvent"));
        var type = current == 0 ? LeftMouseUpType : MacNative.SendNUInt(current, ObjC.Selector("type"));
        var flags = current == 0 ? 0 : MacNative.SendNUInt(current, ObjC.Selector("modifierFlags"));
        if (type == RightMouseUpType || (flags & ControlKeyFlag) != 0)
        {
            ShowMenu();
            return;
        }

        Clicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Attaches a fresh menu, lets the button open it as a click would, and detaches it, so the next left click is a click again.</summary>
    private void ShowMenu()
    {
        _shownEntries = MenuEntries?.Invoke() ?? [];
        if (_shownEntries.Count == 0)
        {
            return;
        }

        var menu = MacNative.SendPtr(MacNative.SendPtr(ObjC.Class("NSMenu"), ObjC.Selector("alloc")), ObjC.Selector("init"));
        try
        {
            MacNative.SendVoid(menu, ObjC.Selector("setAutoenablesItems:"), (byte)0);
            for (var index = 0; index < _shownEntries.Count; index++)
            {
                MacNative.SendVoid(menu, ObjC.Selector("addItem:"), MenuItem(_shownEntries[index], index));
            }

            MacNative.SendVoid(_item, ObjC.Selector("setMenu:"), menu);
            MacNative.SendVoid(_button, ObjC.Selector("performClick:"), (nint)0);
        }
        finally
        {
            MacNative.SendVoid(_item, ObjC.Selector("setMenu:"), (nint)0);
            MacNative.SendVoid(menu, ObjC.Selector("release"));
        }
    }

    /// <summary>An autoreleased NSMenuItem for <paramref name="entry"/>; its tag is the entry's index, which the action reads back.</summary>
    private nint MenuItem(MacMenuEntry entry, int index)
    {
        if (entry.IsSeparator)
        {
            return MacNative.SendPtr(ObjC.Class("NSMenuItem"), ObjC.Selector("separatorItem"));
        }

        var title = Cf.String(entry.Title);
        var empty = Cf.String(string.Empty);
        try
        {
            var item = MacNative.SendPtr(
                MacNative.SendPtr(ObjC.Class("NSMenuItem"), ObjC.Selector("alloc")),
                ObjC.Selector("initWithTitle:action:keyEquivalent:"),
                title,
                ObjC.Selector("augramMenuItem:"),
                empty);
            MacNative.SendVoid(item, ObjC.Selector("setTarget:"), _target);
            MacNative.SendVoid(item, ObjC.Selector("setTag:"), (nint)index);
            MacNative.SendVoid(item, ObjC.Selector("setState:"), entry.IsChecked ? OnState : OffState);
            MacNative.SendVoid(item, ObjC.Selector("setEnabled:"), entry.IsEnabled ? (byte)1 : (byte)0);
            return MacNative.SendPtr(item, ObjC.Selector("autorelease"));
        }
        finally
        {
            Cf.Release(title);
            Cf.Release(empty);
        }
    }
}
