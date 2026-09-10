using System;
using System.Threading.Tasks;

namespace Coms.Desktop.Screens
{
    /// <summary>A screen that loads data when it is shown.</summary>
    internal interface IActivatable
    {
        Task ActivateAsync();
    }

    /// <summary>A screen that reports a title and a status line to the main window.</summary>
    internal interface IStatusSource
    {
        string Title { get; }

        event EventHandler<string> StatusChanged;
    }
}
