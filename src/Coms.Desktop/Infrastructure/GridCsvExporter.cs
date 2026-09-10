using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Coms.Desktop.Infrastructure
{
    /// <summary>Writes the visible columns and rows of a grid to a CSV file, as shown on screen.</summary>
    internal static class GridCsvExporter
    {
        public static bool ExportWithDialog(IWin32Window owner, DataGridView grid, string suggestedName)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Export to CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = suggestedName + ".csv",
                AddExtension = true,
                DefaultExt = "csv"
            })
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return false;
                }

                File.WriteAllText(dialog.FileName, ToCsv(grid), new UTF8Encoding(true));
                return true;
            }
        }

        public static string ToCsv(DataGridView grid)
        {
            var text = new StringBuilder();
            bool first = true;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible)
                {
                    continue;
                }

                if (!first)
                {
                    text.Append(',');
                }

                text.Append(Quote(column.HeaderText));
                first = false;
            }

            text.AppendLine();

            foreach (DataGridViewRow row in grid.Rows)
            {
                first = true;
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!column.Visible)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        text.Append(',');
                    }

                    text.Append(Quote(row.Cells[column.Index].FormattedValue?.ToString() ?? string.Empty));
                    first = false;
                }

                text.AppendLine();
            }

            return text.ToString();
        }

        private static string Quote(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
