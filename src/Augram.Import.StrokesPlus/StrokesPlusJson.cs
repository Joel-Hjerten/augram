namespace Augram.Import.StrokesPlus;

/// <summary>
/// Member names of the StrokesPlus.net JSON document, in one place
/// (<c>docs/reference/strokesplus-net-config.md</c> §2). Readers use these and nothing else.
/// </summary>
public static class StrokesPlusJson
{
    public const string Gestures = "Gestures";

    /// <summary>
    /// The secondary stroke button setting, a WinForms <c>MouseButtons</c> value (<see cref="MouseButtons"/>; 0 = none): the
    /// button a <c>UseSecondaryStrokeButton</c> action holds instead of the stroke button.
    /// </summary>
    public const string SecondaryStrokeButton = "SecondaryStrokeButton";
    public const string GlobalApplication = "GlobalApplication";
    public const string Applications = "Applications";
    public const string IgnoredApplications = "IgnoredApplications";
    public const string Actions = "Actions";

    /// <summary>Members of a <c>Gesture</c> element.</summary>
    public static class Gesture
    {
        public const string Name = "Name";
        public const string Active = "Active";
        public const string MatchCount = "MatchCount";
        public const string PointPatterns = "PointPatterns";
        public const string MultiPointPatterns = "MultiPointPatterns";
    }

    /// <summary>Members of a <c>PointPattern</c> element.</summary>
    public static class PointPattern
    {
        public const string Order = "Order";
        public const string Points = "Points";
    }

    /// <summary>Members of a <c>Point</c> element.</summary>
    public static class Point
    {
        public const string X = "X";
        public const string Y = "Y";
    }

    /// <summary>Members of an <c>Application</c> and an <c>IgnoredApplication</c> element (the matcher fields are shared).</summary>
    public static class Application
    {
        public const string Description = "Description";
        public const string Active = "Active";
        public const string NoGlobalActions = "NoGlobalActions";
        public const string IgnoreFullScreen = "IgnoreFullScreen";
        public const string DisableOnFocus = "DisableOnFocus";
        public const string FileName = "FileName";
        public const string FilePath = "FilePath";
        public const string OwnerClassName = "OwnerClassName";
        public const string RootClassName = "RootClassName";
        public const string ParentClassName = "ParentClassName";
        public const string ControlClassName = "ControlClassName";
        public const string OwnerWindowText = "OwnerWindowText";
        public const string RootWindowText = "RootWindowText";
        public const string ParentWindowText = "ParentWindowText";
        public const string ControlWindowText = "ControlWindowText";
        public const string ControlId = "ControlID";

        /// <summary>The application's category names (an array of strings); <see cref="Action.Category"/> picks one by name.</summary>
        public const string Categories = "Categories";

        /// <summary>Not a member: the category SP.net gives every application. A group whose actions all sit in it imports without categories.</summary>
        public const string DefaultCategory = "General";
    }

    /// <summary>Members of a matcher field (<c>{ Value, IsRegex }</c>).</summary>
    public static class Matcher
    {
        public const string Value = "Value";
        public const string IsRegex = "IsRegex";
    }

    /// <summary>Members of an <c>Action</c> element.</summary>
    public static class Action
    {
        public const string Description = "Description";
        public const string Category = "Category";
        public const string Active = "Active";
        public const string GestureName = "GestureName";
        public const string Control = "Control";
        public const string Alt = "Alt";
        public const string Shift = "Shift";
        public const string Left = "Left";
        public const string Middle = "Middle";
        public const string Right = "Right";
        public const string X1 = "X1";
        public const string X2 = "X2";
        public const string WheelUp = "WheelUp";
        public const string WheelDown = "WheelDown";
        public const string UseSecondaryStrokeButton = "UseSecondaryStrokeButton";

        /// <summary>When the keys and buttons must have gone down: 0 Before, 1 After, 2 Either (learnings 0003 §1, SP.net's order).</summary>
        public const string Capture = "Capture";

        public const string Steps = "Steps";
        public const string Script = "Script";
    }

    /// <summary>WinForms <c>MouseButtons</c> values, as SP.net stores its stroke buttons (reference §2).</summary>
    public static class MouseButtons
    {
        public const long Left = 0x100000;
        public const long Right = 0x200000;
        public const long Middle = 0x400000;
        public const long X1 = 0x800000;
        public const long X2 = 0x1000000;
    }

    /// <summary>Members of a <c>Steps[]</c> element.</summary>
    public static class Step
    {
        public const string Method = "Method";
        public const string Description = "Description";
        public const string Active = "Active";
        public const string MethodParameters = "MethodParameters";
    }

    /// <summary>Members of a <c>MethodParameters[]</c> element. <c>Value</c> may be a string, a number, a bool, an object or null.</summary>
    public static class MethodParameter
    {
        public const string Name = "Name";
        public const string Value = "Value";
    }

    /// <summary>The <c>Steps[].Method</c> names the step reader maps (plan 0001 §C1) and the parameter names it reads.</summary>
    public static class Method
    {
        public const string CloseWindow = "CloseWindow";
        public const string MinimizeWindow = "MinimizeWindow";
        public const string MaximizeOrRestoreWindow = "MaximizeOrRestoreWindow";
        public const string ToggleWindowAlwaysOnTop = "ToggleWindowAlwaysOnTop";
        public const string SetWindowSize = "SetWindowSize";
        public const string InvokeObjectMethodByName = "InvokeObjectMethodByName";
        public const string Delay = "Delay";
        public const string SendVKey = "SendVKey";
        public const string SendHotKey = "SendHotKey";
        public const string SendKeys = "SendKeys";
        public const string SendString = "SendString";
        public const string Run = "Run";
        public const string MouseClick = "MouseClick";
        public const string SendAltDown = "SendAltDown";
        public const string SendAltUp = "SendAltUp";
        public const string SendWinDown = "SendWinDown";
        public const string SendWinUp = "SendWinUp";
        public const string ConsumePhysicalInput = "ConsumePhysicalInput";

        /// <summary>Not an SP.net method: the pseudo-method of the placeholder step a script-only action imports as.</summary>
        public const string Script = "Script";

        public const string WidthParameter = "width";
        public const string HeightParameter = "height";
        public const string MethodNameParameter = "methodName";
        public const string CenterMethodName = "Center";
        public const string MillisecondsParameter = "milliseconds";
        public const string VirtualKeyParameter = "virtualKey";
        public const string ScriptParameter = "script";
        public const string SendKeysParameter = "sendKeysString";
        public const string SendStringParameter = "characters";
    }
}
