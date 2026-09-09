using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Common;
using Dapper;

namespace Coms.Data.Repositories
{
    public abstract class RepositoryBase
    {
        private readonly IDbSession _session;

        protected RepositoryBase(IDbSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        protected IDbTransaction? Transaction => _session.Transaction;

        protected Task<IDbConnection> ConnectionAsync(CancellationToken cancellationToken)
        {
            return _session.GetConnectionAsync(cancellationToken);
        }

        protected CommandDefinition Text(string sql, object? parameters, CancellationToken cancellationToken)
        {
            return new CommandDefinition(sql, parameters, Transaction, cancellationToken: cancellationToken);
        }

        protected CommandDefinition Procedure(string name, object? parameters, CancellationToken cancellationToken)
        {
            return new CommandDefinition(name, parameters, Transaction, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Runs <paramref name="work"/> inside the session's transaction if one
        /// is active, otherwise inside a transaction owned by this call.
        /// </summary>
        protected async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
        {
            if (_session.HasActiveTransaction)
            {
                return await work().ConfigureAwait(false);
            }

            await _session.BeginAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                T result = await work().ConfigureAwait(false);
                await _session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }
            catch
            {
                await _session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Two-statement page query: the page, then the total count over the
        /// same FROM/WHERE. <paramref name="fromAndWhere"/> starts with FROM.
        /// </summary>
        protected async Task<PagedResult<T>> QueryPagedAsync<T>(
            string selectList,
            string fromAndWhere,
            string orderBy,
            DynamicParameters parameters,
            PagedRequest paging,
            CancellationToken cancellationToken)
        {
            string sql =
                "SELECT " + selectList + " " + fromAndWhere +
                " ORDER BY " + orderBy +
                " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;" +
                "SELECT COUNT(*) " + fromAndWhere + ";";

            parameters.Add("@Offset", paging.Offset);
            parameters.Add("@PageSize", paging.PageSize);

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

            using (SqlMapper.GridReader grid = await connection.QueryMultipleAsync(Text(sql, parameters, cancellationToken)).ConfigureAwait(false))
            {
                List<T> items = (await grid.ReadAsync<T>().ConfigureAwait(false)).ToList();
                int total = await grid.ReadSingleAsync<int>().ConfigureAwait(false);
                return new PagedResult<T>(items, total, paging.Page, paging.PageSize);
            }
        }

        /// <summary>
        /// Turns a logical sort key into an ORDER BY clause using a whitelist,
        /// so no user-supplied text ever reaches the SQL. Falls back to
        /// <paramref name="defaultOrderBy"/> for unknown or missing keys.
        /// </summary>
        protected static string ResolveSort(
            PagedRequest paging,
            IReadOnlyDictionary<string, string> sortColumns,
            string defaultOrderBy,
            string tieBreaker)
        {
            if (string.IsNullOrWhiteSpace(paging.SortBy) || !sortColumns.TryGetValue(paging.SortBy!, out string column))
            {
                return defaultOrderBy;
            }

            return column + (paging.SortDescending ? " DESC" : " ASC") + ", " + tieBreaker;
        }

        /// <summary>
        /// Prefix pattern for LIKE with the wildcard characters escaped, or
        /// null when the search text is blank.
        /// </summary>
        protected static string? StartsWith(string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return null;
            }

            string escaped = search!.Trim()
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");

            return escaped + "%";
        }

        protected static Dictionary<string, string> SortMap(params (string Key, string Column)[] entries)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string column) in entries)
            {
                map[key] = column;
            }

            return map;
        }
    }
}
