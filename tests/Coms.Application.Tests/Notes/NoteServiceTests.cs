using System.Threading.Tasks;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Common;
using Coms.Domain.Notes;
using Coms.Domain.Orders;
using Xunit;

namespace Coms.Application.Tests.Notes
{
    public class NoteServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task Add_ToExistingCustomer_SavesTrimmedBody()
        {
            Result<Note> result = await _world.NoteService.AddAsync(NoteEntityType.Customer, _world.ActiveCustomer.Id, "  Prefers morning delivery.  ");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Prefers morning delivery.", result.Value.Body);
            Assert.Equal("tester", result.Value.CreatedBy);
            Assert.Single(await _world.NoteService.GetAsync(NoteEntityType.Customer, _world.ActiveCustomer.Id));
        }

        [Fact]
        public async Task Add_ToMissingOrder_ReturnsNotFound()
        {
            Result<Note> result = await _world.NoteService.AddAsync(NoteEntityType.Order, 42, "x");

            Assert.Equal(ErrorCode.NotFound, result.Code);
            Assert.Empty(_world.Notes.Notes);
        }

        [Fact]
        public async Task Add_ToExistingOrder_Succeeds()
        {
            Order order = _world.SeedOrder(OrderStatus.Draft);

            Result<Note> result = await _world.NoteService.AddAsync(NoteEntityType.Order, order.Id, "Called customer.");

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Add_BlankBody_IsRejected()
        {
            Result<Note> result = await _world.NoteService.AddAsync(NoteEntityType.Customer, _world.ActiveCustomer.Id, "   ");

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "Body");
        }
    }
}
