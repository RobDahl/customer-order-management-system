using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Notes;
using Dapper;

namespace Coms.Data.Repositories
{
    public sealed class NoteRepository : RepositoryBase, INoteRepository
    {
        public NoteRepository(IDbSession session)
            : base(session)
        {
        }

        public async Task<IReadOnlyList<Note>> GetForEntityAsync(NoteEntityType entityType, int entityId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT Id, EntityType, EntityId, Body, CreatedAtUtc, CreatedBy
                  FROM dbo.Notes
                 WHERE EntityType = @EntityType AND EntityId = @EntityId
                 ORDER BY CreatedAtUtc DESC, Id DESC;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<Note>(
                Text(sql, new { EntityType = entityType.ToString(), EntityId = entityId }, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task InsertAsync(Note note, string userName, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT dbo.Notes (EntityType, EntityId, Body, CreatedAtUtc, CreatedBy)
                OUTPUT INSERTED.Id, INSERTED.CreatedAtUtc
                VALUES (@EntityType, @EntityId, @Body, SYSUTCDATETIME(), @UserName);";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            InsertedRow row = await connection.QuerySingleAsync<InsertedRow>(
                Text(sql, new { EntityType = note.EntityType.ToString(), note.EntityId, Body = note.Body.Trim(), UserName = userName }, cancellationToken)).ConfigureAwait(false);

            note.Id = row.Id;
            note.CreatedAtUtc = row.CreatedAtUtc;
            note.CreatedBy = userName;
        }

        private sealed class InsertedRow
        {
            public int Id { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }
    }
}
