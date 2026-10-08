namespace Augram.Core.Steps.TypeText;

/// <summary>
/// How a <see cref="TypeTextStep"/> types (learnings 0001: both are needed, with a per-step switch). Names are
/// stable because they end up in the config file.
/// </summary>
public enum TypeTextMethod
{
    /// <summary>
    /// The characters themselves (<see cref="Abstractions.IInputSimulator.TypeText"/>): any character, any keyboard
    /// layout, works with most apps; some games and consoles that read keys do not see it.
    /// </summary>
    Unicode,

    /// <summary>
    /// The keys that produce the characters on a US layout (<see cref="Abstractions.IInputSimulator.TypeTextByKeys"/>),
    /// for apps that read keys rather than characters; only characters a US keyboard has (<see cref="Abstractions.AsciiKeyLayout"/>).
    /// </summary>
    Keys,
}
