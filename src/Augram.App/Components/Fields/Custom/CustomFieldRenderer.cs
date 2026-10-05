using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Custom;

public sealed class CustomFieldRenderer : IFieldRenderer
{
    public string Kind => "Custom";

    public Control Build(Field field)
    {
        var custom = (CustomField)field;
        var editor = custom.Build();
        if (custom.ViewModel is not null)
        {
            editor.DataContext = custom.ViewModel;
        }

        return editor;
    }
}
