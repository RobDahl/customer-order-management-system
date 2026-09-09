using System;
using System.Collections.Generic;

namespace Coms.Domain.Common
{
    public sealed class PagedResult<T>
    {
        public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }

        public IReadOnlyList<T> Items { get; }

        public int TotalCount { get; }

        public int Page { get; }

        public int PageSize { get; }

        public int TotalPages => TotalCount == 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;

        public bool HasPrevious => Page > 1;

        public bool HasNext => Page < TotalPages;

        /// <summary>1-based index of the first item on this page, 0 when empty.</summary>
        public int FirstItemNumber => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;

        public int LastItemNumber => TotalCount == 0 ? 0 : Math.Min(TotalCount, Page * PageSize);

        public static PagedResult<T> Empty(PagedRequest request)
        {
            return new PagedResult<T>(Array.Empty<T>(), 0, request.Page, request.PageSize);
        }
    }
}
