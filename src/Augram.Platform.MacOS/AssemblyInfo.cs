using System.Runtime.CompilerServices;

// LibraryImport in Interop/MacNative.cs passes blittable values, structs and Span buffers only (bools travel as bytes);
// with runtime marshalling disabled the source generator emits direct calls and refuses anything it cannot marshal itself.
[assembly: DisableRuntimeMarshalling]
