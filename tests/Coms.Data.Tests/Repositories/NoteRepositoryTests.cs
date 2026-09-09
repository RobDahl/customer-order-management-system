using System.Collections.Generic;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Notes;
using Xunit;

namespace Coms.Data.Tests.Repositories
{
    [Collection(DatabaseCollection.Name)]
    public class NoteRepositoryTests
    {
        private readonly DatabaseFixture _db;

        public NoteRepositoryTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task Insert_ThenGetForEntity_ReturnsNewestFirst()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new NoteRepository(session);
                var note = new Note { EntityType = NoteEntityType.Customer, EntityId = 1, Body = "  Called about delivery.  " };

                await repository.InsertAsync(note, "tester");

                Assert.True(note.Id > 0);
                Assert.Equal("tester", note.CreatedBy);

                IReadOnlyList<Note> notes = await repository.GetForEntityAsync(NoteEntityType.Customer, 1);

                Assert.Equal(note.Id, notes[0].Id);
                Assert.Equal("Called about delivery.", notes[0].Body);
                Assert.Equal(NoteEntityType.Customer, notes[0].EntityType);
                for (int i = 1; i < notes.Count; i++)
                {
                    Assert.True(notes[i - 1].CreatedAtUtc >= notes[i].CreatedAtUtc);
                }
            }
        }

        [Fact]
        public async Task GetForEntity_OtherType_DoesNotLeak()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<Note> notes = await new NoteRepository(session).GetForEntityAsync(NoteEntityType.Invoice, 1);

                Assert.Empty(notes);
            }
        }
    }
}
