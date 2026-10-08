using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// The <c>sp.RunProgram</c> script recogniser over synthetic scripts in the shapes a real SP.net config holds: the
/// generated "//Method: RunProgram(…)" comment line and a display-changer call, an elevated hidden taskkill with a trailing
/// blank, SP.net's own <c>String.raw</c> example with quotes in its comments, and <c>sp.ExpandEnvironmentVariables</c>
/// joined to a literal with <c>+</c>; plus everything the recogniser must refuse.
/// </summary>
public sealed class RunProgramScriptTests
{
    private const string MethodComment = "//Method: RunProgram(System.String fileName, System.String arguments, System.String verb, System.String style, System.Boolean useShellExecute, System.Boolean noWindow, System.Boolean waitForExit)";

    [Fact]
    public void AGeneratedCallWithItsCommentIsRecognised()
    {
        var script = MethodComment + "\r\n" + """sp.RunProgram("J:\\Synthetic Drive\\Tools\\Display Tool\\dc64cmd.exe", "-refresh=120", "open", "normal", true, false, false);""";

        var call = Recognize(script);

        Assert.Equal(new RunProgramCall(@"J:\Synthetic Drive\Tools\Display Tool\dc64cmd.exe", "-refresh=120", "open", "normal", true, false, false), call);
        Assert.False(call.IsElevated);
        Assert.False(call.IsHidden);
        Assert.True(call.HasPlainVerb);
    }

    [Fact]
    public void AnElevatedHiddenCallWithATrailingBlankIsRecognised()
    {
        var call = Recognize("""sp.RunProgram("taskkill.exe", "/f /im synthetic-emulator.exe", "runas", "hidden", true, true, false); """);

        Assert.Equal(new RunProgramCall("taskkill.exe", "/f /im synthetic-emulator.exe", "runas", "hidden", true, true, false), call);
        Assert.True(call.IsElevated);
        Assert.True(call.IsHidden);
    }

    [Fact]
    public void StringRawIsReadRawAndCommentsMayHoldQuotes()
    {
        var script = string.Join(
            "\r\n",
            "//An example of passing a string literal (non-escaped string) using: String.raw``",
            "//you would need to use \"C:\\\\Windows\\\\notepad.exe\"",
            "sp.RunProgram(String.raw`C:\\Windows\\notepad.exe`, \"\", \"\", \"\", false, true, false);");

        var call = Recognize(script);

        Assert.Equal(new RunProgramCall(@"C:\Windows\notepad.exe", string.Empty, string.Empty, string.Empty, false, true, false), call);
    }

    [Fact]
    public void ExpandEnvironmentVariablesInTheFileNameKeepsTheVariableForTheRunStep()
    {
        var script = "// Open the Windows File Explorer\r\n" + """sp.RunProgram(sp.ExpandEnvironmentVariables("%SystemRoot%")+"\\explorer.exe", "", "", "", false, false, false);""";

        Assert.Equal(@"%SystemRoot%\explorer.exe", Recognize(script).FileName);
    }

    [Fact]
    public void QuotesEscapesConcatenationAndLayoutAreUnderstood()
    {
        var script = """
            /* block comment */ sp . RunProgram ( 'C:\\Tools\\' + "a\x41\u0042\u{43}.exe" , `--name "x y"` ,
                "op\
            en", "", /* inline */ false, false, true )
            """;

        var call = Recognize(script);

        Assert.Equal(new RunProgramCall(@"C:\Tools\aABC.exe", "--name \"x y\"", "open", string.Empty, false, false, true), call);
    }

    [Theory]
    [InlineData("", "the script is empty")]
    [InlineData("  // only a comment\r\n", "the script is empty")]
    [InlineData("/* never closed", "a comment is not closed")]
    [InlineData("var start = new clr.System.Diagnostics.ProcessStartInfo();\r\n// same as sp.RunProgram\r\nclr.System.Diagnostics.Process.Start(start);", "the script is not a single sp.RunProgram call")]
    [InlineData("""var code = sp.RunProgram("a.exe", "", "", "", true, false, true);""", "the script is not a single sp.RunProgram call")]
    [InlineData("""sp.RunProgramAsync("a.exe", "", "", "", true, false, false);""", "the script is not a single sp.RunProgram call")]
    [InlineData("""sp.RunProgram("a.exe", "", "", "", true, false, false); sp.RunProgram("b.exe", "", "", "", true, false, false);""", "the script does more than one sp.RunProgram call")]
    [InlineData("""sp.RunProgram("a.exe", "", "", "", true, false, false); sp.MessageBox("done", "x");""", "the script does more than one sp.RunProgram call")]
    [InlineData("""sp.RunProgram(path, "", "", "", true, false, false);""", "fileName is not a plain string")]
    [InlineData("""sp.RunProgram("a.exe", `--n ${count}`, "", "", true, false, false);""", "arguments is not a plain string (a template literal with ${…} is not a plain string)")]
    [InlineData("""sp.RunProgram("a.exe", sp.ExpandEnvironmentVariables("%TEMP%"), "", "", true, false, false);""", "arguments is not a plain string")]
    [InlineData("""sp.RunProgram("a.exe", "", "", "", "yes", false, false);""", "useShellExecute is not true or false")]
    [InlineData("""sp.RunProgram("a.exe", "", "", "", true, false);""", "sp.RunProgram has 6 argument(s); it takes 7")]
    [InlineData("""sp.RunProgram("a.exe", "", "", "", true, false, false, 1);""", "sp.RunProgram takes 7 arguments; this call has more")]
    [InlineData("""sp.RunProgram("a.exe" "x", "", "", true, false, false);""", "the sp.RunProgram arguments are not plain values")]
    [InlineData("""sp.RunProgram("C:\\never closed""", "fileName is not a plain string (a string literal is not closed)")]
    [InlineData("sp.RunProgram(\"C:\\\\line\r\nbreak\", \"\", \"\", \"\", true, false, false);", "fileName is not a plain string (a string literal runs past the end of its line)")]
    [InlineData("""sp.RunProgram("bad \x4", "", "", "", true, false, false);""", "fileName is not a plain string (a string literal has a bad \\x or \\u escape)")]
    [InlineData("""sp.RunProgram(String.raw("a"), "", "", "", true, false, false);""", "fileName is not a plain string (String.raw must be followed by a template literal)")]
    public void AnythingElseIsRefusedWithAReason(string script, string expected)
    {
        Assert.False(RunProgramScript.TryRecognize(script, out var call, out var reason));
        Assert.Null(call);
        Assert.Equal(expected, reason);
    }

    private static RunProgramCall Recognize(string script)
    {
        Assert.True(RunProgramScript.TryRecognize(script, out var call, out var reason), reason);
        Assert.Equal(string.Empty, reason);
        return call;
    }
}
