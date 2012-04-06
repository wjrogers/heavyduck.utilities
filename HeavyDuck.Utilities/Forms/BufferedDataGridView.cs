using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HeavyDuck.Utilities.Forms
{
    /// <summary>
    /// A DataGridView with double buffering enabled.
    /// </summary>
    public class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            this.DoubleBuffered = true;
        }
    }
}
