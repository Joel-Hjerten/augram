// The whole spike is Windows-only (P/Invoke, SystemEvents); this silences CA1416 per call site.
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
