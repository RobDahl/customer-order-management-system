using System;
using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Domain.Notes
{
    /// <summary>Append-only free text against a customer, order or invoice.</summary>
    public class Note
    {
        public int Id { get; set; }

        public NoteEntityType EntityType { get; set; }

        public int EntityId { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public IReadOnlyList<ValidationError> Validate()
        {
            var errors = new List<ValidationError>();

            if (EntityId <= 0)
            {
                errors.Add(new ValidationError(nameof(EntityId), "A target record is required."));
            }

            if (string.IsNullOrWhiteSpace(Body))
            {
                errors.Add(new ValidationError(nameof(Body), "Note text is required."));
            }

            return errors;
        }
    }
}
