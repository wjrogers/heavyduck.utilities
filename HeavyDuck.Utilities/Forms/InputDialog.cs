using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace HeavyDuck.Utilities.Forms
{
    public partial class InputDialog : Form
    {
        private InputDialog()
        {
            InitializeComponent();
        }

        public static DialogResult ShowDialog(IWin32Window parent, string title, string prompt, ref string value)
        {
            InputDialog dialog;
            DialogResult result;

            // initialize the dialog
            dialog = new InputDialog();
            dialog.Text = title;
            dialog.prompt_label.Text = prompt;
            dialog.input_box.Text = value;

            // show the dialog
            result = dialog.ShowDialog(parent);

            // set the output and return
            value = dialog.input_box.Text;
            return result;
        }
    }
}
