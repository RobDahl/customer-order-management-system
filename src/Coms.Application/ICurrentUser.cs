namespace Coms.Application
{
    /// <summary>
    /// Identifies who is performing an operation, for audit columns.
    /// The web host resolves this from the signed-in Identity user; the
    /// desktop client from the Windows account.
    /// </summary>
    public interface ICurrentUser
    {
        string UserName { get; }
    }
}
