namespace Augram.App.ViewModels.Commands;

/// <summary>
/// Which sub-tab of the Commands tab a <see cref="CommandsViewModel"/> serves (Joel, 2026-10-07; SP.net
/// keeps Global actions and app actions apart the same way): the Global group's commands, sectioned by
/// its categories, or the app groups' commands, sectioned by group.
/// </summary>
public enum CommandsScope
{
    Global,
    Apps,
}
