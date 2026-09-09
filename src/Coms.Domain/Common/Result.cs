using System;
using System.Collections.Generic;
using System.Linq;

namespace Coms.Domain.Common
{
    /// <summary>
    /// Outcome of an application operation. Business rule failures are
    /// returned, never thrown, so callers handle them as ordinary control
    /// flow and both user interfaces can show the same message.
    /// </summary>
    public class Result
    {
        private static readonly IReadOnlyList<ValidationError> NoErrors = Array.Empty<ValidationError>();

        protected Result(bool isSuccess, ErrorCode code, string message, IReadOnlyList<ValidationError> errors)
        {
            IsSuccess = isSuccess;
            Code = code;
            Message = message;
            Errors = errors;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public ErrorCode Code { get; }

        public string Message { get; }

        /// <summary>Field-level detail when <see cref="Code"/> is <see cref="ErrorCode.Validation"/>.</summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        public static Result Success()
        {
            return new Result(true, ErrorCode.None, string.Empty, NoErrors);
        }

        public static Result Failure(ErrorCode code, string message)
        {
            if (code == ErrorCode.None)
            {
                throw new ArgumentException("A failure needs an error code.", nameof(code));
            }

            return new Result(false, code, message ?? string.Empty, NoErrors);
        }

        public static Result Invalid(IEnumerable<ValidationError> errors)
        {
            var list = errors.ToList();
            string message = list.Count == 1 ? list[0].ToString() : list.Count + " validation errors.";
            return new Result(false, ErrorCode.Validation, message, list);
        }

        public static Result Invalid(string field, string message)
        {
            return Invalid(new[] { new ValidationError(field, message) });
        }

        public static Result NotFound(string what)
        {
            return Failure(ErrorCode.NotFound, what + " was not found.");
        }

        public static Result Conflict()
        {
            return Failure(ErrorCode.Conflict, "The record was changed by another user. Reload and try again.");
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : Code + ": " + Message;
        }
    }

    public sealed class Result<T> : Result
    {
        private readonly T _value;

        private Result(bool isSuccess, T value, ErrorCode code, string message, IReadOnlyList<ValidationError> errors)
            : base(isSuccess, code, message, errors)
        {
            _value = value;
        }

        /// <summary>The value; only meaningful when <see cref="Result.IsSuccess"/> is true.</summary>
        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException("Cannot read the value of a failed result: " + Message);
                }

                return _value;
            }
        }

        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, ErrorCode.None, string.Empty, Array.Empty<ValidationError>());
        }

        public static new Result<T> Failure(ErrorCode code, string message)
        {
            if (code == ErrorCode.None)
            {
                throw new ArgumentException("A failure needs an error code.", nameof(code));
            }

            return new Result<T>(false, default!, code, message ?? string.Empty, Array.Empty<ValidationError>());
        }

        public static new Result<T> Invalid(IEnumerable<ValidationError> errors)
        {
            var list = errors.ToList();
            string message = list.Count == 1 ? list[0].ToString() : list.Count + " validation errors.";
            return new Result<T>(false, default!, ErrorCode.Validation, message, list);
        }

        public static new Result<T> Invalid(string field, string message)
        {
            return Invalid(new[] { new ValidationError(field, message) });
        }

        public static new Result<T> NotFound(string what)
        {
            return Failure(ErrorCode.NotFound, what + " was not found.");
        }

        public static new Result<T> Conflict()
        {
            return Failure(ErrorCode.Conflict, "The record was changed by another user. Reload and try again.");
        }

        /// <summary>Carries a failure across return types without retyping the message.</summary>
        public static Result<T> From(Result failed)
        {
            if (failed.IsSuccess)
            {
                throw new ArgumentException("Only a failed result can be converted.", nameof(failed));
            }

            return new Result<T>(false, default!, failed.Code, failed.Message, failed.Errors);
        }
    }
}
