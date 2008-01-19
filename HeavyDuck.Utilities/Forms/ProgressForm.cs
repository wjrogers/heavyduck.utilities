using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HeavyDuck.Utilities.Forms
{
    internal partial class ProgressForm : Form
    {
        private EventWaitHandle m_wait;

        public ProgressForm(EventWaitHandle handle)
        {
            InitializeComponent();

            m_wait = handle;

            this.Shown += new EventHandler(ProgressForm_Shown);
        }

        private void ProgressForm_Shown(object sender, EventArgs e)
        {
            // when the form becomes visible, let the progress tasks run
            m_wait.Set();
        }

        public string MessageText
        {
            get { return message_label.Text; }
            set { message_label.Text = value; }
        }

        public ProgressBar ProgressBar
        {
            get { return bar; }
        }
    }
}