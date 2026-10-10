using Augram.Platform.Windows.Startup;
using Microsoft.Win32;
using Xunit;

namespace Augram.Platform.Windows.Tests.Startup;

/// <summary>
/// The real registry facade against a throwaway key (<c>HKCU\Software\AugramTest-&lt;guid&gt;</c>), never the real Run or
/// StartupApproved key, so nothing here can start a program at login; the key is deleted afterwards. Skipped on a
/// non-interactive runner.
/// </summary>
public sealed class Win32StartupRegistryTests
{
    [Fact]
    public void ReadsWritesAndDeletesBothValues()
    {
        if (!Environment.UserInteractive)
        {
            return;
        }

        var root = @"Software\AugramTest-" + Guid.NewGuid().ToString("N");
        var runPath = root + @"\Run";
        var approvedPath = root + @"\StartupApproved\Run";
        var registry = new Win32StartupRegistry(runPath, approvedPath);
        try
        {
            Assert.Null(registry.ReadCommand("Augram"));
            Assert.Null(registry.ReadApproval("Augram"));

            registry.WriteCommand("Augram", "\"C:\\x\\Augram.App.exe\" --hidden");
            Assert.Equal("\"C:\\x\\Augram.App.exe\" --hidden", registry.ReadCommand("Augram"));

            byte[] disabled = [0x03, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8];
            using (var key = Registry.CurrentUser.CreateSubKey(approvedPath, writable: true))
            {
                key.SetValue("Augram", disabled, RegistryValueKind.Binary);
                key.SetValue("NotBinary", "text", RegistryValueKind.String);
            }

            Assert.Equal(disabled, registry.ReadApproval("Augram"));
            Assert.Null(registry.ReadApproval("NotBinary"));

            registry.DeleteApproval("Augram");
            registry.DeleteCommand("Augram");
            registry.DeleteApproval("Augram");
            registry.DeleteCommand("Augram");

            Assert.Null(registry.ReadCommand("Augram"));
            Assert.Null(registry.ReadApproval("Augram"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
        }
    }
}
