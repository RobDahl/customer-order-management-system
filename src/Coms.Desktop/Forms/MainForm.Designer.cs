namespace Coms.Desktop.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.mainMenu = new System.Windows.Forms.MenuStrip();
            this.fileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.customersMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.productsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ordersMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.invoicesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reportsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mainToolStrip = new System.Windows.Forms.ToolStrip();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.userStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.databaseStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.recordCountStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.contentPanel = new System.Windows.Forms.Panel();
            this.mainMenu.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // mainMenu
            //
            this.mainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenuItem,
            this.customersMenuItem,
            this.productsMenuItem,
            this.ordersMenuItem,
            this.invoicesMenuItem,
            this.reportsMenuItem,
            this.toolsMenuItem,
            this.helpMenuItem});
            this.mainMenu.Location = new System.Drawing.Point(0, 0);
            this.mainMenu.Name = "mainMenu";
            this.mainMenu.Size = new System.Drawing.Size(1008, 24);
            this.mainMenu.TabIndex = 0;
            //
            // fileMenuItem
            //
            this.fileMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitMenuItem});
            this.fileMenuItem.Name = "fileMenuItem";
            this.fileMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileMenuItem.Text = "&File";
            //
            // exitMenuItem
            //
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
            this.exitMenuItem.Size = new System.Drawing.Size(135, 22);
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            //
            // customersMenuItem
            //
            this.customersMenuItem.Enabled = false;
            this.customersMenuItem.Name = "customersMenuItem";
            this.customersMenuItem.Size = new System.Drawing.Size(76, 20);
            this.customersMenuItem.Text = "&Customers";
            //
            // productsMenuItem
            //
            this.productsMenuItem.Enabled = false;
            this.productsMenuItem.Name = "productsMenuItem";
            this.productsMenuItem.Size = new System.Drawing.Size(66, 20);
            this.productsMenuItem.Text = "&Products";
            //
            // ordersMenuItem
            //
            this.ordersMenuItem.Enabled = false;
            this.ordersMenuItem.Name = "ordersMenuItem";
            this.ordersMenuItem.Size = new System.Drawing.Size(54, 20);
            this.ordersMenuItem.Text = "&Orders";
            //
            // invoicesMenuItem
            //
            this.invoicesMenuItem.Enabled = false;
            this.invoicesMenuItem.Name = "invoicesMenuItem";
            this.invoicesMenuItem.Size = new System.Drawing.Size(62, 20);
            this.invoicesMenuItem.Text = "&Invoices";
            //
            // reportsMenuItem
            //
            this.reportsMenuItem.Enabled = false;
            this.reportsMenuItem.Name = "reportsMenuItem";
            this.reportsMenuItem.Size = new System.Drawing.Size(59, 20);
            this.reportsMenuItem.Text = "&Reports";
            //
            // toolsMenuItem
            //
            this.toolsMenuItem.Enabled = false;
            this.toolsMenuItem.Name = "toolsMenuItem";
            this.toolsMenuItem.Size = new System.Drawing.Size(46, 20);
            this.toolsMenuItem.Text = "&Tools";
            //
            // helpMenuItem
            //
            this.helpMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutMenuItem});
            this.helpMenuItem.Name = "helpMenuItem";
            this.helpMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpMenuItem.Text = "&Help";
            //
            // aboutMenuItem
            //
            this.aboutMenuItem.Name = "aboutMenuItem";
            this.aboutMenuItem.Size = new System.Drawing.Size(116, 22);
            this.aboutMenuItem.Text = "&About...";
            this.aboutMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
            //
            // mainToolStrip
            //
            this.mainToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.mainToolStrip.Location = new System.Drawing.Point(0, 24);
            this.mainToolStrip.Name = "mainToolStrip";
            this.mainToolStrip.Size = new System.Drawing.Size(1008, 25);
            this.mainToolStrip.TabIndex = 1;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.userStatusLabel,
            this.databaseStatusLabel,
            this.recordCountStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 707);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1008, 22);
            this.statusStrip.TabIndex = 2;
            //
            // userStatusLabel
            //
            this.userStatusLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.userStatusLabel.Name = "userStatusLabel";
            this.userStatusLabel.Size = new System.Drawing.Size(36, 17);
            this.userStatusLabel.Text = "User:";
            //
            // databaseStatusLabel
            //
            this.databaseStatusLabel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.databaseStatusLabel.Name = "databaseStatusLabel";
            this.databaseStatusLabel.Size = new System.Drawing.Size(61, 17);
            this.databaseStatusLabel.Text = "Database:";
            //
            // recordCountStatusLabel
            //
            this.recordCountStatusLabel.Name = "recordCountStatusLabel";
            this.recordCountStatusLabel.Size = new System.Drawing.Size(896, 17);
            this.recordCountStatusLabel.Spring = true;
            this.recordCountStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // contentPanel
            //
            this.contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.contentPanel.Location = new System.Drawing.Point(0, 49);
            this.contentPanel.Name = "contentPanel";
            this.contentPanel.Size = new System.Drawing.Size(1008, 658);
            this.contentPanel.TabIndex = 3;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1008, 729);
            this.Controls.Add(this.contentPanel);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainToolStrip);
            this.Controls.Add(this.mainMenu);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MainMenuStrip = this.mainMenu;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Customer Order Management";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.mainMenu.ResumeLayout(false);
            this.mainMenu.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip mainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ToolStripMenuItem customersMenuItem;
        private System.Windows.Forms.ToolStripMenuItem productsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ordersMenuItem;
        private System.Windows.Forms.ToolStripMenuItem invoicesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reportsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutMenuItem;
        private System.Windows.Forms.ToolStrip mainToolStrip;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel userStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel databaseStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel recordCountStatusLabel;
        private System.Windows.Forms.Panel contentPanel;
    }
}
