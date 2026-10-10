using Augram.Core.Mapping;

namespace Augram.App.ViewModels;

/// <summary>
/// One line of the export dialog's selection: an app group (Global included) or an ignored app (<see cref="IsIgnoredApp"/>;
/// its id is a <see cref="GroupId"/> too), its name and a short detail ("8 commands · 1 hold remap · Windows only").
/// </summary>
public sealed record ExportItem(GroupId Id, bool IsIgnoredApp, string Name, string Detail);
