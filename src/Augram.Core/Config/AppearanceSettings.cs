namespace Augram.Core.Config;

/// <summary>
/// How Augram's own windows look, Options › Appearance › Theme (plan 0006, decisions 3, 4, 6 and 10). Local to this machine,
/// like every section of <see cref="Settings"/>, and never in an export (<c>Transfer/</c>). The defaults are the plan's: dark,
/// frosted glass, 62 % tint, 12 px corners, the accent following the trail colour. Rules live in <see cref="SettingsRules"/>.
/// </summary>
public sealed record AppearanceSettings
{
    public const int MaxTintPercent = 100;
    public const int MinCornerRadiusPx = 4;
    public const int MaxCornerRadiusPx = 18;

    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public WindowBackground WindowBackground { get; init; } = WindowBackground.FrostedGlass;

    /// <summary>0 to 100: the opacity of the theme colour over the glass (decision 3; the blur strength is the system's). Unused under <see cref="WindowBackground.Solid"/>.</summary>
    public int TintPercent { get; init; } = 62;

    /// <summary><see cref="MinCornerRadiusPx"/> to <see cref="MaxCornerRadiusPx"/>: panels, tabs, buttons and fields together (decision 4; controls use half). The window's own corners stay the system's.</summary>
    public int CornerRadiusPx { get; init; } = 12;

    /// <summary>The accent is the trail colour ("Same as the trail colour", decision 6); off, it is <see cref="Accent"/>.</summary>
    public bool AccentFollowsTrail { get; init; } = true;

    /// <summary>The accent while <see cref="AccentFollowsTrail"/> is off; Augram's yellow <c>#F5C542</c> until picked (decision 7's shade).</summary>
    public RgbColor Accent { get; init; } = new(245, 197, 66);

    public static AppearanceSettings Default { get; } = new();
}
