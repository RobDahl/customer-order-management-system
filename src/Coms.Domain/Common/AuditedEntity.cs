using System;

namespace Coms.Domain.Common
{
    /// <summary>
    /// Columns every mutable table carries. <see cref="RowVersion"/> is the
    /// SQL Server rowversion used for optimistic concurrency: an update
    /// that supplies a stale value affects no rows.
    /// </summary>
    public abstract class AuditedEntity
    {
        public int Id { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public bool IsNew => Id == 0;
    }
}
