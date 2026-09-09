using Coms.Domain.Common;
using Xunit;

namespace Coms.Domain.Tests.Common
{
    public class PagingTests
    {
        [Fact]
        public void PagedRequest_ClampsPageAndSize()
        {
            var request = new PagedRequest { Page = 0, PageSize = 0 };
            Assert.Equal(1, request.Page);
            Assert.Equal(1, request.PageSize);

            request.PageSize = 10000;
            Assert.Equal(PagedRequest.MaxPageSize, request.PageSize);

            request.Page = 3;
            request.PageSize = 25;
            Assert.Equal(50, request.Offset);
        }

        [Fact]
        public void PagedResult_ComputesNavigation()
        {
            var result = new PagedResult<int>(new[] { 1, 2, 3 }, totalCount: 23, page: 2, pageSize: 10);

            Assert.Equal(3, result.TotalPages);
            Assert.True(result.HasPrevious);
            Assert.True(result.HasNext);
            Assert.Equal(11, result.FirstItemNumber);
            Assert.Equal(20, result.LastItemNumber);
        }

        [Fact]
        public void PagedResult_Empty_IsOnePage()
        {
            PagedResult<int> result = PagedResult<int>.Empty(PagedRequest.FirstPage());

            Assert.Equal(1, result.TotalPages);
            Assert.Equal(0, result.FirstItemNumber);
            Assert.False(result.HasNext);
        }
    }
}
