using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application;
using Coms.Desktop.Infrastructure;
using Coms.Desktop.Screens;

namespace Coms.Desktop.Forms
{
    /// <summary>
    /// The main window: menu, toolbar, status bar and one content panel that
    /// hosts the current list screen. No MDI; detail records open as dialogs.
    /// </summary>
    internal partial class MainForm : Form
    {
        private readonly DesktopSession _session;
        private readonly Scoped _scoped;
        private readonly ComsOptions _options;
        private readonly Dictionary<string, Control> _screens = new Dictionary<string, Control>();

        /* Public on purpose: the dependency injection container only considers public constructors. */
        public MainForm(DesktopSession session, Scoped scoped, ComsOptions options)
        {
            _session = session;
            _scoped = scoped;
            _options = options;
            InitializeComponent();
            WireMenus();
        }

        private void WireMenus()
        {
            customersMenuItem.Enabled = true;
            customersMenuItem.Click += async (s, e) => await ShowAsync("customers", () => new CustomerListScreen(_scoped));

            productsMenuItem.Enabled = true;
            productsMenuItem.Click += async (s, e) => await ShowAsync("products", () => new ProductListScreen(_scoped));

            ordersMenuItem.Enabled = true;
            ordersMenuItem.Click += async (s, e) => await ShowAsync("orders", () => new OrderListScreen(_scoped, _options));

            invoicesMenuItem.Enabled = true;
            invoicesMenuItem.Click += async (s, e) => await ShowAsync("invoices", () => new InvoiceListScreen(_scoped));

            reportsMenuItem.Text = "&Reports (web application)";
            reportsMenuItem.Enabled = false;

            toolsMenuItem.Enabled = true;
            var importItem = new ToolStripMenuItem("&CSV import...");
            importItem.Click += (s, e) =>
            {
                using (var form = new ImportForm(_scoped))
                {
                    form.ShowDialog(this);
                }
            };
            var logsItem = new ToolStripMenuItem("Open &log folder");
            logsItem.Click += (s, e) => OpenLogFolder();
            toolsMenuItem.DropDownItems.Add(importItem);
            toolsMenuItem.DropDownItems.Add(logsItem);

            mainToolStrip.Items.Add(new ToolStripButton("Customers", null, (s, e) => customersMenuItem.PerformClick()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            mainToolStrip.Items.Add(new ToolStripButton("Products", null, (s, e) => productsMenuItem.PerformClick()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            mainToolStrip.Items.Add(new ToolStripButton("Orders", null, (s, e) => ordersMenuItem.PerformClick()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            mainToolStrip.Items.Add(new ToolStripButton("Invoices", null, (s, e) => invoicesMenuItem.PerformClick()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            mainToolStrip.Items.Add(new ToolStripSeparator());
            mainToolStrip.Items.Add(new ToolStripButton("New order", null, async (s, e) => await NewOrderAsync()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            userStatusLabel.Text = "User: " + _session.UserName;
            databaseStatusLabel.Text = "Database: " + _session.DatabaseName;
            recordCountStatusLabel.Text = string.Empty;

            await ShowAsync("orders", () => new OrderListScreen(_scoped, _options));
        }

        private async Task ShowAsync<TScreen>(string key, Func<TScreen> create) where TScreen : Control
        {
            Control screen;
            if (!_screens.TryGetValue(key, out screen))
            {
                screen = create();
                _screens[key] = screen;

                var withStatus = screen as IStatusSource;
                if (withStatus != null)
                {
                    withStatus.StatusChanged += (s, text) => recordCountStatusLabel.Text = text;
                }
            }

            contentPanel.SuspendLayout();
            contentPanel.Controls.Clear();
            contentPanel.Controls.Add(screen);
            contentPanel.ResumeLayout();

            var status = screen as IStatusSource;
            Text = status == null ? Ui.AppTitle : Ui.AppTitle + " - " + status.Title;

            var activatable = screen as IActivatable;
            if (activatable != null)
            {
                await activatable.ActivateAsync();
            }
        }

        private async Task NewOrderAsync()
        {
            await ShowAsync("orders", () => new OrderListScreen(_scoped, _options));
            var orders = _screens["orders"] as OrderListScreen;
            orders?.CreateNewOrder();
        }

        private void OpenLogFolder()
        {
            string directory = Environment.ExpandEnvironmentVariables(
                System.Configuration.ConfigurationManager.AppSettings["Coms:LogDirectory"] ?? "%LOCALAPPDATA%\\Coms\\logs");
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", directory);
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string text = "Customer Order Management System" + Environment.NewLine +
                          "Desktop client version " + version + Environment.NewLine +
                          "Runtime " + Environment.Version + Environment.NewLine +
                          "Database: " + _session.DatabaseName + Environment.NewLine +
                          "User: " + _session.UserName;

            MessageBox.Show(this, text, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
