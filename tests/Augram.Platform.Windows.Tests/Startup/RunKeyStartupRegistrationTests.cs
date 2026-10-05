using Augram.Platform.Windows.Startup;
using Microsoft.Win32;
using Xunit;

namespace Augram.Platform.Windows.Tests.Startup;

/// <summary>Writes a throwaway value under the real HKCU Run key and removes it again; skipped on a non-interactive runner.</summary>
public sealed class RunKeyStartupRegistrationTests
{
    [Fact]
    public void SetWritesAndRemovesTheQuotedCommand()
    {
        if (!Environment.UserInteractive)
        {
            return;
        }

        var name = "AugramTest-" + Guid.NewGuid().ToString("N");
        var registration = new RunKeyStartupRegistration(name);
        try
        {
            Assert.False(registration.IsEnabled);
            Assert.Equal($"\"{Environment.ProcessPath}\"", registration.Command);

            registration.Set(true);
            Assert.True(registration.IsEnabled);
            Assert.Equal(registration.Command, registration.RegisteredCommand);
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyStartupRegistration.RunKeyPath))
            {
                Assert.Equal(registration.Command, key?.GetValue(name));
            }

            registration.Set(false);
            Assert.False(registration.IsEnabled);
            Assert.Null(registration.RegisteredCommand);

            registration.Set(false);
            Assert.False(registration.IsEnabled);
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyStartupRegistration.RunKeyPath, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }

    [Fact]
    public void DefaultsNameTheProductValue()
    {
        var registration = new RunKeyStartupRegistration(command: "\"C:\\x\\Augram.exe\"");

        Assert.Equal(RunKeyStartupRegistration.DefaultValueName, registration.ValueName);
        Assert.Equal("\"C:\\x\\Augram.exe\"", registration.Command);
    }
}
