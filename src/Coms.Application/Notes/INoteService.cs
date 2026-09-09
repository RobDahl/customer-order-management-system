using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Notes;

namespace Coms.Application.Notes
{
    public interface INoteService
    {
        Task<IReadOnlyList<Note>> GetAsync(NoteEntityType entityType, int entityId, CancellationToken cancellationToken = default);

        /// <summary>Adds a note after checking the target record exists.</summary>
        Task<Result<Note>> AddAsync(NoteEntityType entityType, int entityId, string body, CancellationToken cancellationToken = default);
    }
}
