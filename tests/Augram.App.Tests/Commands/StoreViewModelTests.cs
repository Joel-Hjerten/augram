using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class StoreViewModelTests
{
    [Fact]
    public void GuardShowsARefusalOfItsSet_AndLetsAnyOtherExceptionThrough()
    {
        var vm = new Probe(new FakeConfirmPresenter());

        Assert.False(vm.RunGuarded(() => throw new KeyNotFoundException("That command no longer exists.")));
        Assert.Equal("That command no longer exists.", vm.Message);
        Assert.Throws<InvalidOperationException>(() => vm.RunGuarded(() => throw new InvalidOperationException()));
        Assert.True(vm.RunGuarded(() => { }));
    }

    [Fact]
    public async Task DeleteDeclinedRemovesNothing_AndConfirmedSaysWhatWent()
    {
        var confirm = new FakeConfirmPresenter { Answer = false };
        var vm = new Probe(confirm);
        var removed = 0;
        string Remove()
        {
            removed++;
            return "Deleted 'X'.";
        }

        await vm.Delete("Delete command", "Delete command 'X'?", Remove);
        Assert.Equal(0, removed);
        Assert.Null(vm.Message);
        Assert.Equal([("Delete command", "Delete command 'X'?", "Delete")], confirm.Requests);

        confirm.Answer = true;
        await vm.Delete("Delete command", "Delete command 'X'?", Remove);
        Assert.Equal(1, removed);
        Assert.Equal("Deleted 'X'.", vm.Message);
    }

    private sealed class Probe(FakeConfirmPresenter confirm) : StoreViewModel(confirm, MappingRefusals)
    {
        public bool RunGuarded(Action action) => Guard(action);

        public Task Delete(string title, string? question, Func<string> remove) => DeleteAsync(title, question, remove);

        protected override void OnStoreChangedOnUi()
        {
        }
    }
}
