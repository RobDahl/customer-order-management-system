using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Coms.Domain.Notes
{
    public interface INoteRepository
    {
        /// <summary>Notes for one record, newest first.</summary>
        Task<IReadOnlyList<Note>> GetForEntityAsync(NoteEntityType entityType, int entityId, CancellationToken cancellationToken = default);

        Task InsertAsync(Note note, string userName, CancellationToken cancellationToken = default);
    }
}
