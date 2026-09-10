using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Coms.Desktop.Printing
{
    /// <summary>
    /// Prints a list of pre-formatted text lines in a monospace font with
    /// simple pagination and a page footer. Documents are laid out as text
    /// columns, so they print the same on any printer and read fine in
    /// black and white.
    /// </summary>
    internal sealed class TextDocumentPrinter
    {
        private readonly IList<string> _lines;
        private readonly string _title;
        private int _nextLine;
        private int _page;

        public TextDocumentPrinter(string title, IList<string> lines)
        {
            _title = title;
            _lines = lines;
        }

        public void Preview(IWin32Window owner)
        {
            using (PrintDocument document = CreateDocument())
            using (var dialog = new PrintPreviewDialog { Document = document, Width = 900, Height = 700, UseAntiAlias = true })
            {
                dialog.Text = _title;
                dialog.ShowDialog(owner);
            }
        }

        private PrintDocument CreateDocument()
        {
            var document = new PrintDocument { DocumentName = _title };
            document.DefaultPageSettings.Margins = new Margins(60, 60, 60, 60);
            document.BeginPrint += (s, e) => { _nextLine = 0; _page = 0; };
            document.PrintPage += PrintPage;
            return document;
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            _page++;
            using (var font = new Font("Courier New", 9.5F))
            using (var footerFont = new Font("Courier New", 8F))
            {
                float lineHeight = font.GetHeight(e.Graphics);
                float y = e.MarginBounds.Top;
                float bottom = e.MarginBounds.Bottom - lineHeight * 2;

                while (_nextLine < _lines.Count && y + lineHeight <= bottom)
                {
                    e.Graphics.DrawString(_lines[_nextLine], font, Brushes.Black, e.MarginBounds.Left, y);
                    y += lineHeight;
                    _nextLine++;
                }

                string footer = _title + "    Page " + _page;
                e.Graphics.DrawString(footer, footerFont, Brushes.Black, e.MarginBounds.Left, e.MarginBounds.Bottom - lineHeight);
                e.HasMorePages = _nextLine < _lines.Count;
            }
        }
    }

    /// <summary>Helpers for fixed-width text layout.</summary>
    internal static class TextLayout
    {
        public const int Width = 92;

        public static string Left(string text, int width)
        {
            text = text ?? string.Empty;
            return text.Length > width ? text.Substring(0, width) : text.PadRight(width);
        }

        public static string Right(string text, int width)
        {
            text = text ?? string.Empty;
            return text.Length > width ? text.Substring(0, width) : text.PadLeft(width);
        }

        public static string Rule(char c = '-')
        {
            return new string(c, Width);
        }

        public static IEnumerable<string> Wrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string remaining = paragraph;
                while (remaining.Length > width)
                {
                    int cut = remaining.LastIndexOf(' ', width);
                    if (cut <= 0)
                    {
                        cut = width;
                    }

                    yield return remaining.Substring(0, cut).TrimEnd();
                    remaining = remaining.Substring(cut).TrimStart();
                }

                yield return remaining;
            }
        }
    }
}
