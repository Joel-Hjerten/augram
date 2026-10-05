using Augram.Core.Config;

namespace Augram.Core.Tests.Config;

/// <summary>An <see cref="IConfigStore"/> that hands out a fixed document and counts saves.</summary>
internal sealed class InMemoryConfigStore : IConfigStore
{
    public InMemoryConfigStore(ConfigDocument loaded)
    {
        Loaded = loaded;
    }

    public ConfigDocument Loaded { get; }

    public List<ConfigDocument> Saved { get; } = [];

    public Exception? SaveFailure { get; set; }

    public string Location => "memory://augram.json";

    public ConfigDocument Load() => Loaded;

    public void Save(ConfigDocument document)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        Saved.Add(document);
    }
}
