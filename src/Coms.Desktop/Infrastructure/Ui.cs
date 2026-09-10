using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Coms.Domain.Common;

namespace Coms.Desktop.Infrastructure
{
    /// <summary>Message boxes and small control helpers used by every screen.</summary>
    internal static class Ui
    {
        public const string AppTitle = "Customer Order Management";

        public static void Info(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Warn(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Error(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void Error(IWin32Window owner, Exception exception)
        {
            MessageBox.Show(owner, "An unexpected error occurred:" + Environment.NewLine + Environment.NewLine + exception.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool Confirm(IWin32Window owner, string question)
        {
            return MessageBox.Show(owner, question, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        /// <summary>Shows a failed result. Validation errors are listed one per line.</summary>
        public static void ShowFailure(IWin32Window owner, Result result)
        {
            if (result.IsSuccess)
            {
                return;
            }

            if (result.Code == ErrorCode.Validation && result.Errors.Count > 0)
            {
                var text = new StringBuilder("Please correct the following:").AppendLine().AppendLine();
                foreach (ValidationError error in result.Errors)
                {
                    text.Append("- ").Append(error.Message).Append("  [").Append(error.Field).Append(']').AppendLine();
                }

                Warn(owner, text.ToString());
                return;
            }

            if (result.Code == ErrorCode.Conflict)
            {
                Warn(owner, result.Message + Environment.NewLine + Environment.NewLine + "The record will be reloaded.");
                return;
            }

            Warn(owner, result.Message);
        }

        public static Label MakeLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 6, 3, 3) };
        }

        public static TextBox MakeTextBox(int width, int maxLength = 0)
        {
            var box = new TextBox { Width = width, Anchor = AnchorStyles.Left };
            if (maxLength > 0)
            {
                box.MaxLength = maxLength;
            }

            return box;
        }

        public static Button MakeButton(string text, EventHandler onClick = null, int width = 90)
        {
            var button = new Button { Text = text, Width = width, Height = 26, Margin = new Padding(3), UseVisualStyleBackColor = true };
            if (onClick != null)
            {
                button.Click += onClick;
            }

            return button;
        }

        public static string Money(decimal value)
        {
            return value.ToString("N2");
        }

        public static string Quantity(decimal value)
        {
            return value.ToString("0.###");
        }

        /// <summary>Disables a control while an async operation runs and shows the wait cursor.</summary>
        public static IDisposable Busy(Control control)
        {
            return new BusyScope(control);
        }

        private sealed class BusyScope : IDisposable
        {
            private readonly Control _control;
            private readonly bool _wasEnabled;
            private readonly Cursor _cursor;

            public BusyScope(Control control)
            {
                _control = control;
                _wasEnabled = control.Enabled;
                _cursor = control.Cursor;
                control.Enabled = false;
                control.Cursor = Cursors.WaitCursor;
            }

            public void Dispose()
            {
                _control.Enabled = _wasEnabled;
                _control.Cursor = _cursor;
            }
        }
    }
}
