using Augram.App.Components.HeldInputList;
using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The Exclusions › While holding half of <see cref="IgnoredViewModel"/> (plan 0012): the selected entry's "Ignore gestures while
/// holding" card. Its rows are the side in view (<see cref="HeldInputs"/>, each named in that platform's words with its F8
/// marker, <see cref="StepPlatformMarker.For(WhileHoldingInput, HostPlatform)"/>, and the hold-key warning,
/// <see cref="HoldKeyConflicts.ForEntry"/>); which side is in view is a <see cref="PlatformSideView"/> (opens on this platform,
/// back to it on another selection, never stored). Each intent of the card is one store call, one undo step
/// (<c>MappingStore.WhileHolding</c>): on this platform's side Add, Delete, Reorder and Paste edit it (the first edit on an
/// inferred side makes it its own, and the message line says so); on the other side only the switch, Copy from (the whole list,
/// either side) and a row's Copy to (that input converted, added here) reach the store. A refused edit shows Core's rule.
/// </summary>
public sealed partial class IgnoredViewModel
{
    private PlatformSideView? _holdingView;
    private Guid? _holdingFor;
    private KeyOrButton? _copiedInput;

    /// <summary>This sub-tab is Exclusions › While holding: its entries have the "Ignore gestures while holding" card.</summary>
    public bool IsWhileHolding => Scope == IgnoreScope.WhileHolding;

    /// <summary>Where Augram runs: the side the card opens on and the only one edited.</summary>
    public HostPlatform Platform => _platform;

    /// <summary>The rows of the side in view; empty while no While holding entry is selected.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<HeldInputItem> HeldInputs { get; private set; } = [];

    /// <summary>The "Ignore for" row: the side in view, its note and Copy from; null while no While holding entry is selected (the card is hidden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsHeldInputs))]
    public partial PlatformSide? HeldInputSide { get; private set; }

    /// <summary>The side in view is the other platform's: read-only.</summary>
    [ObservableProperty]
    public partial bool HeldInputsReadOnly { get; private set; }

    /// <summary>"Copy to Windows" on each row of the other platform's side; null on this one.</summary>
    [ObservableProperty]
    public partial string? HeldInputCopyToText { get; private set; }

    /// <summary>The selected row of the card, which its keys and menu act on; -1 for none.</summary>
    [ObservableProperty]
    public partial int SelectedHeldInput { get; private set; } = -1;

    /// <summary>The card shows: a While holding entry is selected.</summary>
    public bool ShowsHeldInputs => HeldInputSide is not null;

    private PlatformSideView HoldingView => _holdingView ??= new PlatformSideView(_platform);

    /// <summary>One intent of the card, run inside <c>Guard</c> after the message line is cleared.</summary>
    public void HandleHeldInput(HeldInputListActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        Guard(() => DispatchHeld(e));
    }

    private void DispatchHeld(HeldInputListActionEventArgs e)
    {
        switch (e.Action)
        {
            case HeldInputListAction.Undo:
                _store.Undo();
                return;
            case HeldInputListAction.Redo:
                _store.Redo();
                return;
            case HeldInputListAction.ShowSide when e.Platform is { } platform:
                HoldingView.Shown = platform;
                SelectedHeldInput = -1;
                ProjectDetail();
                return;
        }

        if (SelectedId is not { } selected || Find(selected) is not { IsWhileHolding: true } entry)
        {
            return;
        }

        var shown = HoldingView.Shown;
        switch (e.Action)
        {
            case HeldInputListAction.CopyFromOtherPlatform:
                _store.CopyHeldInputsFromOtherPlatform(entry.Id, shown);
                Message = $"The {shown.DisplayName()} list of '{entry.Name}' is the {shown.Other().DisplayName()} list now. {Undo}";
                return;
            case HeldInputListAction.CopyTo when e.Item is { } item && !HoldingView.IsHere:
                var forked = entry.Holding.IsInferredOn(_platform);
                var copied = _store.CopyHeldInputFromOtherPlatform(entry.Id, _platform, item.Index);
                var added = copied.Holding.For(_platform)[^1].Input.Describe(_platform);
                Message = $"Added {added} to the {_platform.DisplayName()} list of '{entry.Name}'.{OwnSentence(forked, entry)} {Undo}";
                return;
        }

        // Everything else edits this platform's side, which is only ever the one in view here.
        if (!HoldingView.IsHere)
        {
            return;
        }

        var inferred = entry.Holding.IsInferredOn(_platform);
        var rows = entry.Holding.EditableFor(_platform);
        switch (e.Action)
        {
            case HeldInputListAction.Select when e.Item is { } item:
                SelectedHeldInput = item.Index;
                return;
            case HeldInputListAction.Add when e.Input is { } input:
                Add(entry, input, inferred);
                return;
            case HeldInputListAction.Paste:
                if (_copiedInput is not { } pasted)
                {
                    Message = "Copy an input first.";
                    return;
                }

                Add(entry, pasted, inferred);
                return;
            case HeldInputListAction.Copy when e.Item is { } item && item.Index < rows.Count:
                _copiedInput = rows[item.Index].ForPlatform(_platform) ?? rows[item.Index].Input;
                Message = $"Copied {_copiedInput.Describe(_platform)}; paste it on another While holding app's list.";
                return;
            case HeldInputListAction.Delete when e.Item is { } item:
                _store.RemoveHeldInput(entry.Id, _platform, item.Index);
                SelectedHeldInput = Math.Min(item.Index, rows.Count - 2);
                Message = $"Removed {item.Name}.{OwnSentence(inferred, entry)} {Undo}";
                return;
            case HeldInputListAction.Reorder when e.Item is { } item && e.TargetIndex >= 0:
                _store.MoveHeldInput(entry.Id, _platform, item.Index, e.TargetIndex);
                SelectedHeldInput = e.TargetIndex;
                if (inferred)
                {
                    Message = $"{OwnSentence(inferred, entry).TrimStart()} {Undo}";
                }

                return;
        }
    }

    private void Add(IgnoredApp entry, KeyOrButton input, bool inferred)
    {
        var stored = _store.AddHeldInput(entry.Id, _platform, input);
        SelectedHeldInput = stored.Holding.For(_platform).Count - 1;
        if (inferred)
        {
            Message = $"{OwnSentence(inferred, entry).TrimStart()} {Undo}";
        }
    }

    /// <summary>" 'After Effects' now has its own macOS list; the Windows list keeps its own.", after the first edit on an inferred side; else nothing.</summary>
    private string OwnSentence(bool inferred, IgnoredApp entry)
        => inferred ? $" '{entry.Name}' now has its own {_platform.DisplayName()} list; the {_platform.Other().DisplayName()} list keeps its own." : string.Empty;

    private static string Undo => $"{EditKeymap.Current.Undo} undoes it.";

    /// <summary>
    /// The card of <paramref name="app"/> (null, or an entry on another list: no card): the side in view, back on this platform
    /// when another entry is selected, its rows and the "Ignore for" row.
    /// </summary>
    private void ProjectHeldInputs(IgnoredApp? app)
    {
        if (app is not { IsWhileHolding: true })
        {
            _holdingFor = null;
            HeldInputSide = null;
            HeldInputs = [];
            SelectedHeldInput = -1;
            return;
        }

        if (_holdingFor != app.Id.Value)
        {
            _holdingFor = app.Id.Value;
            HoldingView.Reset();
            SelectedHeldInput = -1;
        }

        var view = HoldingView;
        var shown = view.Shown;
        var rows = app.Holding.For(shown);
        var conflicts = HoldKeyConflicts.ForEntry(_store.Current, app.Id, shown);
        HeldInputs = [.. rows.Select((row, index) =>
        {
            var text = StepPlatformMarker.For(row, shown);
            var warning = row.ForPlatform(shown) is KeyOrButton.Key key ? conflicts.FirstOrDefault(conflict => conflict.Key == key.KeyCode)?.OnEntry(shown) : null;
            return new HeldInputItem(index, text.Summary, text.Marker, warning);
        })];
        HeldInputsReadOnly = !view.IsHere;
        // An inferred side is this platform's list converted: copying a row back would only add what is here already.
        HeldInputCopyToText = view.IsHere || app.Holding.IsInferredOn(shown) ? null : $"Copy to {_platform.DisplayName()}";
        HeldInputSide = new PlatformSide(
            shown,
            view.IsHere,
            view.IsHere ? Capitalised(StepPlatformMarker.ForInputs(rows, shown)) : PlatformSideView.EditNote(shown),
            app.Holding.Own is null ? null : $"Copy from {shown.Other().DisplayName()}");
        if (SelectedHeldInput >= rows.Count)
        {
            SelectedHeldInput = rows.Count - 1;
        }
    }

    private static string? Capitalised(string? marker) => string.IsNullOrEmpty(marker) ? null : char.ToUpperInvariant(marker[0]) + marker[1..];
}
