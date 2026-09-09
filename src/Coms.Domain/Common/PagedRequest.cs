using System;

namespace Coms.Domain.Common
{
    /// <summary>
    /// Page, page size and sort for list screens. Sort keys are logical
    /// names; each repository maps them to SQL from a fixed whitelist, so
    /// user input never reaches an ORDER BY clause directly.
    /// </summary>
    public sealed class PagedRequest
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 500;

        private int _page = 1;
        private int _pageSize = DefaultPageSize;

        public int Page
        {
            get => _page;
            set => _page = Math.Max(1, value);
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Math.Min(MaxPageSize, Math.Max(1, value));
        }

        public string? SortBy { get; set; }

        public bool SortDescending { get; set; }

        public int Offset => (Page - 1) * PageSize;

        public static PagedRequest FirstPage(int pageSize = DefaultPageSize)
        {
            return new PagedRequest { Page = 1, PageSize = pageSize };
        }
    }
}
