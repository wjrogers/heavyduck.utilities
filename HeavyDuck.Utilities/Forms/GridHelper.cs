using System;
using System.Collections.Generic;
using System.Data;
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
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.AutoGenerateColumns = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.Font = new Font("Verdana", 8);
            grid.GridColor = SystemColors.InactiveBorder;
            grid.ReadOnly = readOnly;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 18;
        }

        public static void AddColumn(DataGridView grid, string name, string header)
        {
            AddColumn(grid, new DataGridViewTextBoxColumn(), name, header);
        }

        public static void AddColumn(DataGridView grid, DataGridViewColumn column, string name, string header)
        {
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

        /// <summary>
        /// Gets the DataRow bound to a DataGridViewRow.
        /// </summary>
        public static DataRow GetBoundDataRow(DataGridView grid, int rowIndex)
        {
            DataRowView view;

            try
            {
                view = grid.Rows[rowIndex].DataBoundItem as DataRowView;
                if (view != null)
                    return view.Row;
                else
                    return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}
