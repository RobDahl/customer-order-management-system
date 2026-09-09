using System.ComponentModel;

// The compiler looks for this type when it compiles init-only setters.
// It ships with .NET 5+ but not with .NET Standard 2.0, so the shared
// libraries declare it themselves. Marked internal so that consumers that
// already have the runtime type do not see a duplicate.
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
