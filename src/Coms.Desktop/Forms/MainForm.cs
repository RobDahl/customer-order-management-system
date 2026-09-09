using System;
using System.Reflection;
using System.Windows.Forms;

namespace Coms.Desktop.Forms
{
    public partial class MainForm : Form
    {
        private readonly DesktopSession _session;

        public MainForm(DesktopSession session)
        {
            _session = session;
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            userStatusLabel.Text = "User: " + _session.UserName;
            databaseStatusLabel.Text = "Database: " + _session.DatabaseName;
            recordCountStatusLabel.Text = string.Empty;
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
                          "Database: " + _session.DatabaseName + Environment.NewLine +
                          "User: " + _session.UserName;

            MessageBox.Show(this, text, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
