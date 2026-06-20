// Polyfill for the compiler-required IsExternalInit type used by records and init-only setters.
// The type ships in net5.0+; netstandard2.0 needs this internal copy.
#if !NET5_0_OR_GREATER

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
