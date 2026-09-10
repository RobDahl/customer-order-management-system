using System.Drawing;
using System.Windows.Forms;

namespace Coms.Desktop.Infrastructure
{
    /// <summary>Standard DataGridView setup per the UI style guide: read-only, full-row select, zebra rows, no fancy rendering.</summary>
    internal static class GridHelper
    {
        public static readonly Color ZebraColor = Color.FromArgb(0xF7, 0xF7, 0xF7);

        public static void Configure(DataGridView grid, bool readOnly = true)
        {
            grid.AutoGenerateColumns = false;
            grid.ReadOnly = readOnly;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AlternatingRowsDefaultCellStyle.BackColor = ZebraColor;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.BackgroundColor = SystemColors.Window;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.Dock = DockStyle.Fill;
        }

        public static DataGridViewTextBoxColumn AddColumn(DataGridView grid, string header, string property, int width, bool alignRight = false, string format = null, bool readOnly = true)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = property,
                Name = property,
                Width = width,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            if (alignRight)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (format != null)
            {
                column.DefaultCellStyle.Format = format;
            }

            grid.Columns.Add(column);
            return column;
        }

        public static DataGridViewCheckBoxColumn AddCheckColumn(DataGridView grid, string header, string property, int width)
        {
            var column = new DataGridViewCheckBoxColumn
            {
                HeaderText = header,
                DataPropertyName = property,
                Name = property,
                Width = width,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            grid.Columns.Add(column);
            return column;
        }

        public static T SelectedItem<T>(DataGridView grid) where T : class
        {
            return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as T;
        }
    }
}
