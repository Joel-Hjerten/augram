using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// The screen says where hold remaps work (Joel, 2026-10-10, plan 0005 decision 7): the Exclusions › Global list's ⓘ and an
/// entry's form ("Hold remaps still work in this app: they never use the stroke button."; in the disable-while-focused mode
/// "Augram is off while this app has focus, hold remaps included."), and a hold remap form's ⓘ.
/// </summary>
public sealed class ExclusionsTextTests
{
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);

    [AvaloniaFact]
    public void TheGlobalListSaysHoldRemapsStillWorkThere()
    {
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], [Blender, VMware]));
        var global = new IgnoredViewModel(store, new FakeFormDialogPresenter(), new FakeConfirmPresenter(), HostPlatform.Windows);
        var perCommand = new IgnoredViewModel(store, new FakeFormDialogPresenter(), new FakeConfirmPresenter(), HostPlatform.Windows, IgnoreScope.PerCommand);

        Assert.Contains(IgnoredModes.HoldRemapsInTheseApps, global.Help, StringComparison.Ordinal);
        Assert.Contains("Also in", global.Help, StringComparison.Ordinal);
        Assert.DoesNotContain(IgnoredModes.HoldRemapsInTheseApps, perCommand.Help, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AnEntrysFormSaysWhatItsModeMeansForHoldRemaps()
    {
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], [Blender, VMware]));
        var vm = new IgnoredViewModel(store, new FakeFormDialogPresenter(), new FakeConfirmPresenter(), HostPlatform.Windows);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, vm.Items.Single(item => item.Name == "Blender")));
        var note = HoldRemapsNote(vm.Detail!);
        Assert.Equal("Hold remaps still work in this app: they never use the stroke button.", note.Text.Get());
        Assert.Equal(["Name", "Active", "Mode", IgnoredModes.HoldRemapsLabel, IgnoredEditViewModel.AllowedForLabel], vm.Detail!.Sections[0].Fields.Select(field => field.Label));

        // The mode switched on the form: the note follows it.
        Fields(vm.Detail!).OfType<ButtonRadioField<bool>>().Single().Value.Set(true);
        Assert.Equal("Augram is off while this app has focus, hold remaps included.", note.Text.Get());

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, vm.Items.Single(item => item.Name == "VMware")));
        Assert.Equal(IgnoredModes.OffWhileFocused, HoldRemapsNote(vm.Detail!).Text.Get());
    }

    [AvaloniaFact]
    public void AHoldRemapFormSaysItWorksOverAGlobalExclusion()
    {
        var form = HoldRemapEditViewModel.From(HoldRemap.For(KeyCode.Space)).Declare();

        Assert.EndsWith("Works even where the app is on Exclusions › Global.", form.Sections[0].Help, StringComparison.Ordinal);
    }

    private static IEnumerable<Field> Fields(FormScreen screen) => screen.Sections.SelectMany(section => section.Fields);

    private static NoteField HoldRemapsNote(FormScreen screen) => Fields(screen).OfType<NoteField>().Single(field => field.Label == IgnoredModes.HoldRemapsLabel);
}
