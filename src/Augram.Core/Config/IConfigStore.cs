namespace Augram.Core.Config;

/// <summary>
/// Port for configuration persistence (ADR-0002 §2; kept beside the document rather than in
/// <c>Abstractions/</c> because nothing outside Core implements it). <see cref="FileConfigStore"/>
/// is the production implementation; tests use in-memory ones. <see cref="Load"/> never throws
/// for a missing or unreadable file: it falls back and reports how, so the app always starts.
/// </summary>
public interface IConfigStore
{
    /// <summary>Where the document lives, for display ("config file: ...") and "open folder".</summary>
    string Location { get; }

    ConfigDocument Load();

    void Save(ConfigDocument document);
}
