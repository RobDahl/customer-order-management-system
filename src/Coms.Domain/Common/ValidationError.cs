using System;

namespace Coms.Domain.Common
{
    public sealed class ValidationError
    {
        public ValidationError(string field, string message)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>Property name the message refers to; empty for whole-object errors.</summary>
        public string Field { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Field.Length == 0 ? Message : Field + ": " + Message;
        }
    }
}
