using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;


namespace HeavyDuck.Utilities.Forms
{
    public static class GridHelper
    {
        /// <summary>
        /// Initializes a DataGridView with my favorite default properties.
        /// </summary>
        /// <param name="grid">The grid to initialize.</param>
        /// <param name="readOnly">Whether the grid should be read-only.</param>
        public static void Initialize(DataGridView grid, bool readOnly)
        {
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToOrderColumns = false;
            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.Font = new Font("Verdana", 8);
            grid.ReadOnly = readOnly;
            grid.RowHeadersVisible = false;
        }

        public static void AddColumn(DataGridView grid, string name, string header)
        {
            DataGridViewColumn column = new DataGridViewTextBoxColumn();

            column.Name = name;
            column.DataPropertyName = name;
            column.HeaderText = header;

            grid.Columns.Add(column);
        }

        public static void DisableClickToSort(DataGridView grid, bool showSortArrow)
        {
            foreach (DataGridViewColumn column in grid.Columns)
                column.SortMode = showSortArrow ? DataGridViewColumnSortMode.Programmatic : DataGridViewColumnSortMode.NotSortable;
        }
    }
}
