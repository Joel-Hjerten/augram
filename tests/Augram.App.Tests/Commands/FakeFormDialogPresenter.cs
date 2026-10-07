using Augram.App.Components.FormDialog;

namespace Augram.App.Tests.Commands;

/// <summary>Records every dialog request and answers through <see cref="Answer"/> (true by default), which may also fill the request's form through its bindings.</summary>
public sealed class FakeFormDialogPresenter : IFormDialogPresenter
{
    public List<FormDialogRequest> Requests { get; } = [];

    public Func<FormDialogRequest, bool> Answer { get; set; } = _ => true;

    public FormDialogRequest Last => Requests[^1];

    public Task<bool> ShowAsync(FormDialogRequest request)
    {
        Requests.Add(request);
        return Task.FromResult(Answer(request));
    }
}
