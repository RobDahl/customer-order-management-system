using System.Collections.Generic;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Coms.Web.Helpers
{
    /// <summary>
    /// Sortable column headers and pager links that keep the rest of the
    /// current query string intact.
    /// </summary>
    public static class ListHtmlHelpers
    {
        public static IHtmlContent SortLink(this IHtmlHelper html, string text, string key)
        {
            HttpRequest request = html.ViewContext.HttpContext.Request;
            string currentKey = request.Query["sortBy"].ToString();
            bool currentDesc = string.Equals(request.Query["desc"].ToString(), "true", System.StringComparison.OrdinalIgnoreCase);
            bool isCurrent = string.Equals(currentKey, key, System.StringComparison.OrdinalIgnoreCase);

            // Clicking the current column flips direction; clicking another sorts ascending.
            bool nextDesc = isCurrent && !currentDesc;

            string url = QueryUrl(request, new Dictionary<string, string?>
            {
                ["sortBy"] = key,
                ["desc"] = nextDesc ? "true" : null,
                ["page"] = null
            });

            var link = new TagBuilder("a");
            link.Attributes["href"] = url;
            link.AddCssClass("sort");
            if (isCurrent)
            {
                link.AddCssClass("sort-current");
            }

            link.InnerHtml.Append(text);
            if (isCurrent)
            {
                // Constant markup, so it may bypass encoding; the text above is encoded.
                link.InnerHtml.AppendHtml(currentDesc ? " ▼" : " ▲");
            }

            return link;
        }

        public static string PageUrl(this IHtmlHelper html, int page)
        {
            HttpRequest request = html.ViewContext.HttpContext.Request;
            return QueryUrl(request, new Dictionary<string, string?> { ["page"] = page.ToString() });
        }

        /// <summary>Current path with the given query values replaced (null removes the key).</summary>
        public static string QueryUrl(HttpRequest request, IDictionary<string, string?> overrides)
        {
            var values = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, StringValues> pair in request.Query)
            {
                values[pair.Key] = pair.Value.ToString();
            }

            foreach (KeyValuePair<string, string?> pair in overrides)
            {
                if (pair.Value == null)
                {
                    values.Remove(pair.Key);
                }
                else
                {
                    values[pair.Key] = pair.Value;
                }
            }

            return QueryHelpers.AddQueryString(request.Path, values);
        }
    }
}
