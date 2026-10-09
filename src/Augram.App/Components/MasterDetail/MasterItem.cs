namespace Augram.App.Components.MasterDetail;

/// <summary>
/// One row of a <see cref="MasterDetail"/> list: its identity, name, active flag and a one-line summary under the name
/// ("Gestures off over this app · blender.exe"). Plain data the host builds from its store; the component never reads a store.
/// </summary>
public sealed record MasterItem(Guid Id, string Name, bool IsActive, string Summary);
