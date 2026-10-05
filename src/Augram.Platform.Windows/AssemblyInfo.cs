using System.Runtime.CompilerServices;

// LibraryImport in Interop/NativeMethods.cs passes Span<char> buffers and blittable structs only; with runtime
// marshalling disabled the source generator emits direct pointer calls and refuses anything it cannot marshal itself.
[assembly: DisableRuntimeMarshalling]
