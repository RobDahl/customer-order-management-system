using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Coms.Data.Connections
{
    /// <summary>
    /// Opens database connections for repositories. Keeping this behind an
    /// interface lets the web host, the desktop client and the integration
    /// tests each supply their own connection string without the
    /// repositories knowing where it came from.
    /// </summary>
    public interface IDbConnectionFactory
    {
        Task<IDbConnection> OpenAsync(CancellationToken cancellationToken = default);
    }
}
