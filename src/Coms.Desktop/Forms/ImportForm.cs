using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Csv;
using Coms.Desktop.Infrastructure;

namespace Coms.Desktop.Forms
{
    /// <summary>
    /// CSV import: pick the kind and file, "Check" runs a dry run and lists
    /// every problem by row and column, "Import" writes the file in one
    /// transaction. A file with any error imports nothing.
    /// </summary>
    internal sealed class ImportForm : Form
    {
        private readonly Scoped _scoped;
        private readonly RadioButton _customers = new RadioButton { Text = "Customers", AutoSize = true, Checked = true };
        private readonly RadioButton _products = new RadioButton { Text = "Products", AutoSize = true };
        private readonly TextBox _path = Ui.MakeTextBox(420);
        private readonly CheckBox _updateExisting = new CheckBox { Text = "Update existing records (matched by customer number / SKU)", AutoSize = true };
        private readonly Label _summary = new Label { AutoSize = true, Margin = new Padding(8, 8, 8, 4), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        private readonly DataGridView _errors = new DataGridView();
        private readonly Button _check;
        private readonly Button _import;

        public ImportForm(Scoped scoped)
        {
            _scoped = scoped;

            Text = "CSV import";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 520);

            var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));

            var kinds = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            kinds.Controls.Add(_customers);
            kinds.Controls.Add(_products);
            AddRow(top, "Data", kinds);

            var filePanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            filePanel.Controls.Add(_path);
            filePanel.Controls.Add(Ui.MakeButton("Browse...", (s, e) => Browse(), 80));
            filePanel.Controls.Add(Ui.MakeButton("Save template...", (s, e) => SaveTemplate(), 110));
            AddRow(top, "File", filePanel);
            AddRow(top, string.Empty, _updateExisting);

            GridHelper.Configure(_errors);
            GridHelper.AddColumn(_errors, "Row", nameof(CsvRowError.RowNumber), 55, alignRight: true);
            GridHelper.AddColumn(_errors, "Column", nameof(CsvRowError.Field), 160);
            GridHelper.AddColumn(_errors, "Problem", nameof(CsvRowError.Message), 480);

            var results = new GroupBox { Text = "Result", Dock = DockStyle.Fill, Padding = new Padding(6) };
            results.Controls.Add(_errors);
            results.Controls.Add(_summary);
            _summary.Dock = DockStyle.Top;

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
            Button close = Ui.MakeButton("Close", (s, e) => Close());
            _import = Ui.MakeButton("Import", async (s, e) => await RunAsync(dryRun: false));
            _check = Ui.MakeButton("Check file", async (s, e) => await RunAsync(dryRun: true));
            buttons.Controls.Add(close);
            buttons.Controls.Add(_import);
            buttons.Controls.Add(_check);
            CancelButton = close;

            Controls.Add(results);
            Controls.Add(top);
            Controls.Add(buttons);
        }

        private void Browse()
        {
            using (var dialog = new OpenFileDialog { Title = "Choose a CSV file", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _path.Text = dialog.FileName;
                }
            }
        }

        private async void SaveTemplate()
        {
            try
            {
                string header = await _scoped.RunAsync<ICsvImportService, string>(s => Task.FromResult(_products.Checked ? s.ProductTemplate : s.CustomerTemplate));
                using (var dialog = new SaveFileDialog { Title = "Save template", Filter = "CSV files (*.csv)|*.csv", FileName = (_products.Checked ? "products" : "customers") + "-template.csv" })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        File.WriteAllText(dialog.FileName, header + "\r\n", new UTF8Encoding(true));
                    }
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private async Task RunAsync(bool dryRun)
        {
            if (!File.Exists(_path.Text))
            {
                Ui.Warn(this, "Choose an existing CSV file.");
                return;
            }

            if (!dryRun && !Ui.Confirm(this, "Import " + Path.GetFileName(_path.Text) + " now? Rows are written in one transaction; a file with any error imports nothing."))
            {
                return;
            }

            var options = new CsvImportOptions { UpdateExisting = _updateExisting.Checked, DryRun = dryRun };

            try
            {
                using (Ui.Busy(this))
                {
                    CsvImportResult result;
                    using (var reader = new StreamReader(_path.Text, Encoding.UTF8, true))
                    {
                        bool products = _products.Checked;
                        result = await _scoped.RunAsync<ICsvImportService, CsvImportResult>(
                            s => products ? s.ImportProductsAsync(reader, options) : s.ImportCustomersAsync(reader, options));
                    }

                    _summary.Text = result.Summary;
                    _summary.ForeColor = result.HasErrors ? Color.FromArgb(0xB0, 0x00, 0x20) : result.Committed ? Color.FromArgb(0x2E, 0x7D, 0x32) : SystemColors.ControlText;
                    _errors.DataSource = new List<CsvRowError>(result.Errors);
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Ui.MakeLabel(label), 0, row);
            control.Margin = new Padding(3);
            table.Controls.Add(control, 1, row);
        }
    }
}
