using System;
using Coms.Domain.Common;
using Xunit;

namespace Coms.Domain.Tests.Common
{
    public class ResultTests
    {
        [Fact]
        public void Success_HasNoCodeOrErrors()
        {
            Result result = Result.Success();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(ErrorCode.None, result.Code);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Failure_RequiresACode()
        {
            Assert.Throws<ArgumentException>(() => Result.Failure(ErrorCode.None, "x"));
        }

        [Fact]
        public void Invalid_SingleError_UsesItAsMessage()
        {
            Result result = Result.Invalid("Name", "Name is required.");

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Equal("Name: Name is required.", result.Message);
            Assert.Single(result.Errors);
        }

        [Fact]
        public void Invalid_ManyErrors_SummarisesCount()
        {
            Result result = Result.Invalid(new[] { new ValidationError("A", "a"), new ValidationError("B", "b") });

            Assert.Equal("2 validation errors.", result.Message);
        }

        [Fact]
        public void GenericValue_ThrowsOnFailure()
        {
            Result<int> result = Result<int>.NotFound("Order");

            Assert.Equal(ErrorCode.NotFound, result.Code);
            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        [Fact]
        public void GenericSuccess_ExposesValue()
        {
            Assert.Equal(42, Result<int>.Success(42).Value);
        }

        [Fact]
        public void From_CarriesFailureAcrossTypes()
        {
            Result failed = Result.Failure(ErrorCode.Conflict, "stale");

            Result<string> converted = Result<string>.From(failed);

            Assert.Equal(ErrorCode.Conflict, converted.Code);
            Assert.Equal("stale", converted.Message);
            Assert.Throws<ArgumentException>(() => Result<string>.From(Result.Success()));
        }
    }
}
