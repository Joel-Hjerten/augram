namespace Augram.Core.Config;

/// <summary>Options › Appearance › Theme (plan 0006 decision 10): the colours of Augram's own windows.</summary>
public enum AppTheme
{
    /// <summary>The default (requirements F7: dark from the start).</summary>
    Dark,

    Light,

    /// <summary>Follow the operating system's light or dark setting ("Follow system").</summary>
    System,
}
