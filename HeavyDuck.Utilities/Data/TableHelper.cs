using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace HeavyDuck.Utilities.Data
{
    public static class TableHelper
    {
        public static Nullable<T> GetNullableValue<T>(object value) where T : struct
        {
            return value == null || Convert.IsDBNull(value) ? null : (Nullable<T>)value;
        }

        public static DataTable GroupBy(DataTable table, string groupBy, params string[] sumColumns)
        {
            string[] groupNames;
            List<DataColumn> sumDataColumns;
            List<DataColumn> groupDataColumns;
            Dictionary<string, DataRow> resultRows;
            DataTable result;

            // prepare column lists
            sumDataColumns = new List<DataColumn>(sumColumns.Length);
            foreach (string name in sumColumns)
            {
                if (!sumDataColumns.Contains(table.Columns[name]))
                    sumDataColumns.Add(table.Columns[name]);
            }
            groupNames = groupBy.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            groupDataColumns = new List<DataColumn>(groupNames.Length);
            foreach (string name in groupNames)
            {
                if (!groupDataColumns.Contains(table.Columns[name]))
                    groupDataColumns.Add(table.Columns[name]);
            }

            // create result table and row dictionary
            result = new DataTable(table.TableName + " Grouped");
            foreach (DataColumn column in groupDataColumns)
                result.Columns.Add(column.ColumnName, column.DataType);
            foreach (DataColumn column in sumDataColumns)
                result.Columns.Add(column.ColumnName, column.DataType);
            result.PrimaryKey = new DataColumn[] { result.Columns[groupBy] };
            resultRows = new Dictionary<string, DataRow>();

            // do it
            foreach (DataRow sourceRow in table.Rows)
            {
                DataRow resultRow;
                StringBuilder sb;
                string key;

                // get the group values
                sb = new StringBuilder();
                foreach (DataColumn column in groupDataColumns)
                {
                    sb.Append(sourceRow[column].ToString());
                    sb.Append(", ");
                }
                key = sb.ToString();

                // read the group-by value and find or create the matching result column
                if (resultRows.ContainsKey(key))
                {
                    resultRow = resultRows[key];
                }
                else
                {
                    resultRow = result.NewRow();
                    foreach (DataColumn column in groupDataColumns)
                        resultRow[column.ColumnName] = sourceRow[column];
                    foreach (DataColumn column in sumDataColumns)
                        resultRow[column.ColumnName] = 0;

                    resultRows[key] = resultRow;
                }

                // add stuff
                foreach (DataColumn column in sumDataColumns)
                {
                    resultRow[column.ColumnName] = Convert.ChangeType(Convert.ToDecimal(resultRow[column.ColumnName]) + Convert.ToDecimal(sourceRow[column]), column.DataType);
                }
            }

            // add all the result rows to the result table (we delay this to avoid creating a bunch of row versions above, which is slow)
            result.BeginLoadData();
            foreach (DataRow row in resultRows.Values)
                result.Rows.Add(row);
            result.EndLoadData();

            // ok I guess we did it
            return result;
        }
    }
}
