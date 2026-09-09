using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Notes;
using Coms.Domain.Orders;

namespace Coms.Application.Notes
{
    public sealed class NoteService : INoteService
    {
        private readonly INoteRepository _notes;
        private readonly ICustomerRepository _customers;
        private readonly IOrderRepository _orders;
        private readonly IInvoiceRepository _invoices;
        private readonly ICurrentUser _currentUser;

        public NoteService(
            INoteRepository notes,
            ICustomerRepository customers,
            IOrderRepository orders,
            IInvoiceRepository invoices,
            ICurrentUser currentUser)
        {
            _notes = notes ?? throw new ArgumentNullException(nameof(notes));
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public Task<IReadOnlyList<Note>> GetAsync(NoteEntityType entityType, int entityId, CancellationToken cancellationToken = default)
        {
            return _notes.GetForEntityAsync(entityType, entityId, cancellationToken);
        }

        public async Task<Result<Note>> AddAsync(NoteEntityType entityType, int entityId, string body, CancellationToken cancellationToken = default)
        {
            var note = new Note { EntityType = entityType, EntityId = entityId, Body = (body ?? string.Empty).Trim() };

            IReadOnlyList<ValidationError> errors = note.Validate();
            if (errors.Count > 0)
            {
                return Result<Note>.Invalid(errors);
            }

            bool exists = await TargetExistsAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                return Result<Note>.NotFound(entityType.ToString());
            }

            await _notes.InsertAsync(note, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            return Result<Note>.Success(note);
        }

        private async Task<bool> TargetExistsAsync(NoteEntityType entityType, int entityId, CancellationToken cancellationToken)
        {
            switch (entityType)
            {
                case NoteEntityType.Customer:
                    return await _customers.GetByIdAsync(entityId, cancellationToken).ConfigureAwait(false) != null;
                case NoteEntityType.Order:
                    return await _orders.GetByIdAsync(entityId, cancellationToken).ConfigureAwait(false) != null;
                case NoteEntityType.Invoice:
                    return await _invoices.GetByIdAsync(entityId, cancellationToken).ConfigureAwait(false) != null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown note target.");
            }
        }
    }
}
