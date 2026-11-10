// netstandard2.1 does not ship System.Runtime.CompilerServices.IsExternalInit,
// which is required by the compiler for records and init-only setters.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
