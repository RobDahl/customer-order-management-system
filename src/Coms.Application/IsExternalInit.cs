using System.ComponentModel;

// Needed to consume init-only properties (CsvHelper's configuration) from
// .NET Standard 2.0, which does not ship this marker type.
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
