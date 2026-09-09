using Coms.Domain.Common;

namespace Coms.Web.ViewModels
{
    /// <summary>
    /// Query-string parameters every list screen shares. Bound from the URL
    /// so a filtered, sorted page can be bookmarked or sent to a colleague.
    /// </summary>
    public abstract class ListQuery
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = PagedRequest.DefaultPageSize;

        public string? SortBy { get; set; }

        public bool Desc { get; set; }

        public PagedRequest ToPagedRequest()
        {
            return new PagedRequest { Page = Page, PageSize = PageSize, SortBy = SortBy, SortDescending = Desc };
        }
    }

    /// <summary>What the pager partial needs, independent of the row type.</summary>
    public sealed class PagerModel
    {
        public int Page { get; set; }

        public int TotalPages { get; set; }

        public int TotalCount { get; set; }

        public int FirstItem { get; set; }

        public int LastItem { get; set; }

        public static PagerModel From<T>(PagedResult<T> result)
        {
            return new PagerModel
            {
                Page = result.Page,
                TotalPages = result.TotalPages,
                TotalCount = result.TotalCount,
                FirstItem = result.FirstItemNumber,
                LastItem = result.LastItemNumber
            };
        }
    }
}
