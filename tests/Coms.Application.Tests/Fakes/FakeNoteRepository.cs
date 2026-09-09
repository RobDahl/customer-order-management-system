using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Notes;

namespace Coms.Application.Tests.Fakes
{
    internal sealed class FakeNoteRepository : INoteRepository
    {
        public List<Note> Notes { get; } = new List<Note>();

        public Task<IReadOnlyList<Note>> GetForEntityAsync(NoteEntityType entityType, int entityId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Note> rows = Notes.Where(n => n.EntityType == entityType && n.EntityId == entityId).OrderByDescending(n => n.Id).ToList();
            return Task.FromResult(rows);
        }

        public Task InsertAsync(Note note, string userName, CancellationToken cancellationToken = default)
        {
            note.Id = Notes.Count + 1;
            note.CreatedBy = userName;
            note.CreatedAtUtc = DateTime.UtcNow;
            Notes.Add(note);
            return Task.CompletedTask;
        }
    }
}
