using System.Collections.Frozen;
using CoreKey = Augram.Core.Abstractions.KeyCode;
using HookKey = SharpHook.Data.KeyCode;

namespace Augram.Engine.Input;

/// <summary>
/// Core <see cref="CoreKey"/> to SharpHook <see cref="HookKey"/> and back. Built once by name:
/// Core's member names are SharpHook's without the <c>Vc</c> prefix (digits: <c>Digit0</c> is
/// <c>Vc0</c>). A Core member with no SharpHook counterpart maps to <see cref="HookKey.VcUndefined"/>
/// and the simulator reports it unsupported; <c>tests/Augram.Engine.Tests/Input/KeyCodeMapTests</c>
/// fails if any member is in that situation.
/// </summary>
public static class KeyCodeMap
{
    private const string Prefix = "Vc";
    private const string DigitPrefix = "Digit";

    private static readonly FrozenDictionary<CoreKey, HookKey> ToHookTable = Build();
    private static readonly FrozenDictionary<HookKey, CoreKey> ToCoreTable =
        ToHookTable.Where(pair => pair.Value != HookKey.VcUndefined).ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    public static HookKey ToHook(CoreKey key) => ToHookTable.TryGetValue(key, out var hook) ? hook : HookKey.VcUndefined;

    /// <summary><see cref="CoreKey.None"/> for keys Core has no name for (IME keys, VcUndefined).</summary>
    public static CoreKey ToCore(HookKey key) => ToCoreTable.TryGetValue(key, out var core) ? core : CoreKey.None;

    /// <summary>The Core keys that have no SharpHook counterpart; empty is the expectation the tests enforce.</summary>
    public static IReadOnlyList<CoreKey> Unmapped() =>
        ToHookTable.Where(pair => pair.Key != CoreKey.None && pair.Value == HookKey.VcUndefined).Select(pair => pair.Key).ToList();

    private static FrozenDictionary<CoreKey, HookKey> Build()
    {
        var table = new Dictionary<CoreKey, HookKey>();
        foreach (var key in Enum.GetValues<CoreKey>())
        {
            table[key] = key == CoreKey.None ? HookKey.VcUndefined : Lookup(key.ToString());
        }

        return table.ToFrozenDictionary();
    }

    private static HookKey Lookup(string coreName)
    {
        var name = coreName.StartsWith(DigitPrefix, StringComparison.Ordinal) ? coreName[DigitPrefix.Length..] : coreName;
        return Enum.TryParse<HookKey>(Prefix + name, ignoreCase: false, out var hook) ? hook : HookKey.VcUndefined;
    }
}
