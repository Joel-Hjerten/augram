using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Platform.MacOS.Interop;
using Augram.Platform.MacOS.WindowSystem;

namespace Augram.Platform.MacOS.Overlay;

/// <summary>
/// The trail's own window on macOS (F6, 2026-10-09): a borderless, non-activating <c>NSPanel</c> with a
/// <c>CAShapeLayer</c> as its content, because an ordinary <c>NSWindow</c> from the UI toolkit never shows over another
/// app's full-screen Space (Chrome › YouTube full screen showed no trail). The panel is made by the first
/// <see cref="Verify"/>, never before: click-through (<c>ignoresMouseEvents = YES</c>) and the rest are set at creation and
/// read back by every <see cref="Verify"/>, and re-asserted by every <see cref="Cover"/> and <see cref="Park"/> (CLAUDE.md
/// invariant 6). Status-window level (over the Dock and the menu bar), every Space and full-screen apps as an auxiliary,
/// stationary, out of the Cmd-` cycle, never key or main, not hidden when Augram is inactive (a panel's default), no
/// shadow, no ordering animation. Coordinates are global top-left points (<see cref="MacRect"/>); the layer's are
/// bottom-left (<see cref="MacTrailGeometry"/>). Every path change runs in a <c>CATransaction</c> with actions disabled,
/// so the line never animates. Main thread only, as AppKit requires; <see cref="Dispose"/> hops there itself.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacTrailPanel : ITrailSurface
{
    // NSWindowStyleMaskBorderless (0) | NSWindowStyleMaskNonactivatingPanel. The non-activating bit only works when the panel is created with it.
    private const nuint NonactivatingPanel = 1 << 7;
    private const nuint BackingStoreBuffered = 2;

    // NSStatusWindowLevel: above the Dock (20) and the main menu (24).
    private const nint StatusWindowLevel = 25;

    // NSWindowCollectionBehavior: CanJoinAllSpaces | Stationary | IgnoresCycle | FullScreenAuxiliary.
    private const nuint IgnoresCycle = 1 << 6;
    private const nuint Behavior = (1 << 0) | (1 << 4) | IgnoresCycle | (1 << 8);

    // NSWindowAnimationBehaviorNone.
    private const nint NoAnimation = 2;

    private readonly CGPointBuffer _points = new(1024);
    private nint _panel;
    private nint _layer;
    private MacRect _area = new(0, 0, 1, 1);
    private bool _disposed;

    public OverlayStyleReport Verify()
    {
        if (_panel == 0 && !_disposed)
        {
            Create();
        }

        if (_panel == 0)
        {
            return new OverlayStyleReport(false, false, false, "no NSPanel");
        }

        ApplyClickThrough();
        var clickThrough = MacNative.SendBool(_panel, ObjC.Selector("ignoresMouseEvents")) != 0;
        var level = MacNative.SendNInt(_panel, ObjC.Selector("level"));
        var behavior = MacNative.SendNUInt(_panel, ObjC.Selector("collectionBehavior"));
        var styleMask = MacNative.SendNUInt(_panel, ObjC.Selector("styleMask"));
        var isKey = MacNative.SendBool(_panel, ObjC.Selector("isKeyWindow")) != 0;
        var isMain = MacNative.SendBool(_panel, ObjC.Selector("isMainWindow")) != 0;
        return new OverlayStyleReport(
            clickThrough,
            NoActivate: !isKey && !isMain && (styleMask & NonactivatingPanel) != 0,
            ToolWindow: (behavior & IgnoresCycle) != 0,
            $"panel ignoresMouseEvents={(clickThrough ? 1 : 0)} level={level} behavior=0x{behavior:X} styleMask=0x{styleMask:X}");
    }

    public TrailSurfaceArea Cover(CapturePoint strokeStart)
    {
        var screens = MacScreens.All();
        var area = MacTrailGeometry.ScreenFor(screens, strokeStart.X, strokeStart.Y);
        Place(area, screens.Count > 0 ? screens[0].Frame.Height : area.Height);
        return new TrailSurfaceArea(area.X, area.Y, area.Width, area.Height);
    }

    public void Park() => Place(new MacRect(0, 0, 1, 1), MacScreens.MainHeight());

    public void Show()
    {
        if (_panel != 0)
        {
            MacNative.SendVoid(_panel, ObjC.Selector("orderFrontRegardless"));
        }
    }

    public void Hide()
    {
        if (_panel != 0)
        {
            MacNative.SendVoid(_panel, ObjC.Selector("orderOut:"), (nint)0);
        }
    }

    public void SetStyle(TrailSettings style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (_layer == 0)
        {
            return;
        }

        var colour = MacNative.CGColorCreateSRGB(style.Colour.R / 255.0, style.Colour.G / 255.0, style.Colour.B / 255.0, Math.Clamp(style.Opacity, 0, 1));
        using (Transaction())
        {
            MacNative.SendVoid(_layer, ObjC.Selector("setStrokeColor:"), colour);
            // Points: AppKit scales them to the display itself.
            MacNative.SendVoid(_layer, ObjC.Selector("setLineWidth:"), Math.Max(style.WidthPx, 0.5));
        }

        MacNative.CGColorRelease(colour);
    }

    public void SetPoints(IReadOnlyList<CapturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (_layer == 0)
        {
            return;
        }

        nint path = 0;
        if (points.Count >= 2)
        {
            var span = _points.Fill(points, _area);
            path = MacNative.CGPathCreateMutable();
            MacNative.CGPathAddLines(path, 0, span, (nuint)span.Length);
        }

        using (Transaction())
        {
            MacNative.SendVoid(_layer, ObjC.Selector("setPath:"), path);
        }

        if (path != 0)
        {
            MacNative.CGPathRelease(path);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_panel != 0)
        {
            MainThread.TryInvoke(Close, out _);
        }
    }

    private bool Close()
    {
        MacNative.SendVoid(_panel, ObjC.Selector("orderOut:"), (nint)0);
        MacNative.SendVoid(_panel, ObjC.Selector("close"));
        MacNative.SendVoid(_panel, ObjC.Selector("release"));
        MacNative.SendVoid(_layer, ObjC.Selector("release"));
        _panel = 0;
        _layer = 0;
        return true;
    }

    private void Create()
    {
        var panelClass = ObjC.Class("NSPanel");
        var layerClass = ObjC.Class("CAShapeLayer");
        if (panelClass == 0 || layerClass == 0)
        {
            return;
        }

        using var pool = ObjC.Pool();
        var start = new MacNative.CGRect { X = 0, Y = 0, Width = 1, Height = 1 };
        var panel = MacNative.SendPtr(
            MacNative.SendPtr(panelClass, ObjC.Selector("alloc")),
            ObjC.Selector("initWithContentRect:styleMask:backing:defer:"),
            start,
            NonactivatingPanel,
            BackingStoreBuffered,
            (byte)0);
        if (panel == 0)
        {
            return;
        }

        MacNative.SendVoid(panel, ObjC.Selector("setIgnoresMouseEvents:"), (byte)1);
        MacNative.SendVoid(panel, ObjC.Selector("setReleasedWhenClosed:"), (byte)0);
        MacNative.SendVoid(panel, ObjC.Selector("setHidesOnDeactivate:"), (byte)0);
        MacNative.SendVoid(panel, ObjC.Selector("setBecomesKeyOnlyIfNeeded:"), (byte)1);
        MacNative.SendVoid(panel, ObjC.Selector("setOpaque:"), (byte)0);
        MacNative.SendVoid(panel, ObjC.Selector("setBackgroundColor:"), MacNative.SendPtr(ObjC.Class("NSColor"), ObjC.Selector("clearColor")));
        MacNative.SendVoid(panel, ObjC.Selector("setHasShadow:"), (byte)0);
        MacNative.SendVoid(panel, ObjC.Selector("setLevel:"), StatusWindowLevel);
        MacNative.SendVoid(panel, ObjC.Selector("setCollectionBehavior:"), Behavior);
        MacNative.SendVoid(panel, ObjC.Selector("setAnimationBehavior:"), NoAnimation);
        MacNative.SendVoid(panel, ObjC.Selector("setExcludedFromWindowsMenu:"), (byte)1);
        var title = Cf.String("Augram trail");
        MacNative.SendVoid(panel, ObjC.Selector("setTitle:"), title);
        Cf.Release(title);

        // A layer-hosting content view: the shape layer is the view's layer, so it is resized with the panel.
        var layer = MacNative.SendPtr(MacNative.SendPtr(layerClass, ObjC.Selector("layer")), ObjC.Selector("retain"));
        MacNative.SendVoid(layer, ObjC.Selector("setFillColor:"), (nint)0);
        MacNative.SendVoid(layer, ObjC.Selector("setLineCap:"), Cf.Constant("round"));
        MacNative.SendVoid(layer, ObjC.Selector("setLineJoin:"), Cf.Constant("round"));
        var view = MacNative.SendPtr(MacNative.SendPtr(ObjC.Class("NSView"), ObjC.Selector("alloc")), ObjC.Selector("initWithFrame:"), start);
        MacNative.SendVoid(view, ObjC.Selector("setLayer:"), layer);
        MacNative.SendVoid(view, ObjC.Selector("setWantsLayer:"), (byte)1);
        MacNative.SendVoid(panel, ObjC.Selector("setContentView:"), view);
        MacNative.SendVoid(view, ObjC.Selector("release"));

        _panel = panel;
        _layer = layer;
    }

    private void ApplyClickThrough()
    {
        MacNative.SendVoid(_panel, ObjC.Selector("setIgnoresMouseEvents:"), (byte)1);
        MacNative.SendVoid(_panel, ObjC.Selector("setLevel:"), StatusWindowLevel);
        MacNative.SendVoid(_panel, ObjC.Selector("setCollectionBehavior:"), Behavior);
    }

    /// <summary>Sets the frame in global top-left points; the borderless panel may cover the menu bar. Click-through is re-asserted first: it costs nothing and must never lapse.</summary>
    private void Place(MacRect area, double mainHeight)
    {
        _area = area;
        if (_panel == 0)
        {
            return;
        }

        MacNative.SendVoid(_panel, ObjC.Selector("setIgnoresMouseEvents:"), (byte)1);
        var cocoa = new MacNative.CGRect { X = area.X, Y = area.CocoaY(mainHeight), Width = area.Width, Height = area.Height };
        MacNative.SendVoid(_panel, ObjC.Selector("setFrame:display:"), cocoa, (byte)0);
        // A layer-hosting view leaves the scale to its owner: without this the line is drawn at 1x and blurs on a Retina display.
        var scale = MacNative.SendDouble(_panel, ObjC.Selector("backingScaleFactor"));
        using (Transaction())
        {
            MacNative.SendVoid(_layer, ObjC.Selector("setContentsScale:"), scale > 0 ? scale : 1);
        }
    }

    private static TransactionScope Transaction() => new();

    /// <summary><c>[CATransaction begin]</c> with implicit animations off, committed on dispose.</summary>
    private readonly struct TransactionScope : IDisposable
    {
        public TransactionScope()
        {
            var transaction = ObjC.Class("CATransaction");
            MacNative.SendVoid(transaction, ObjC.Selector("begin"));
            MacNative.SendVoid(transaction, ObjC.Selector("setDisableActions:"), (byte)1);
        }

        public void Dispose() => MacNative.SendVoid(ObjC.Class("CATransaction"), ObjC.Selector("commit"));
    }

    /// <summary>A reused buffer of layer points, grown as a stroke gets longer.</summary>
    private sealed class CGPointBuffer(int capacity)
    {
        private MacNative.CGPoint[] _items = new MacNative.CGPoint[capacity];

        public ReadOnlySpan<MacNative.CGPoint> Fill(IReadOnlyList<CapturePoint> points, MacRect area)
        {
            if (_items.Length < points.Count)
            {
                _items = new MacNative.CGPoint[Math.Max(points.Count, _items.Length * 2)];
            }

            for (var i = 0; i < points.Count; i++)
            {
                var (x, y) = MacTrailGeometry.ToLayer(points[i].X, points[i].Y, area);
                _items[i] = new MacNative.CGPoint { X = x, Y = y };
            }

            return _items.AsSpan(0, points.Count);
        }
    }
}
