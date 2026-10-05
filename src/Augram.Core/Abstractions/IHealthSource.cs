using Augram.Core.Diagnostics;

namespace Augram.Core.Abstractions;

/// <summary>The health summary at the top of the Diagnostics tab (N4). Read rarely (on display refresh), never on a hot path.</summary>
public interface IHealthSource
{
    HealthSnapshot Current();
}
