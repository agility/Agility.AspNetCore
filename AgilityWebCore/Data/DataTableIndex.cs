using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Agility.Web;

namespace Agility.Web
{
	/// <summary>
	///		Allows quick lookups on DataTables based on column values instead of using the super
	///		slow Select method of a DataTable.
	/// </summary>
	public class DataTableIndex
	{
		private DataView lookupTable = null;

		public DataTableIndex(DataTable data, params string[] index)
		{
			DataColumn[] cols = new DataColumn[index.Length];

			for (int i = 0; i < index.Length; i++)
			{
				if (!data.Columns.Contains(index[i]))
				{
					throw new ApplicationException(string.Format("The column {0} does not exist in this DataTable.", index[i]));
				}

				cols[i] = data.Columns[index[i]];
			}


			createIndex(data, cols);
		}

		public DataTableIndex(DataTable data, params DataColumn[] index)
		{
			createIndex(data, index);
		}

		private void createIndex(DataTable data, DataColumn[] index)
		{
			bool first = true;
			string sort = "";

			// create a comma separated list of sort columns
			foreach (DataColumn column in index)
			{
				if (!first)
				{
					sort += ",";
				}

				first = false;

				// use brackets to handle column names with spaces, etc
				sort += "[" + column.ColumnName + "]";
			}

			// use a DataView because it internally creates an index to cover the sort criteria
			lookupTable = new DataView(
				data,
				null,
				sort,
				DataViewRowState.CurrentRows);
		}

		/// <summary>
		///		Searches for DataRow's using an indexed lookup.
		/// </summary>
		/// <param name="value">
		///		Value order must directly match the order of the columns passed in to the constructor.
		/// </param>
		/// <returns>
		///		The matching DataRow's.
		/// </returns>
		public DataRow[] Find(params object[] value)
		{
			DataRowView[] found = lookupTable.FindRows(value);

			DataRow[] matchingRows = new DataRow[found.Length];

			for (int i = 0; i < found.Length; i++)
			{
				matchingRows[i] = found[i].Row;
			}

			return matchingRows;
		}

		public DataRow[] FindAll(IEnumerable<object> values)
		{
			List<DataRow> lst = new List<DataRow>();

			foreach (object value in values)
			{

				DataRowView[] found = lookupTable.FindRows(value);
				DataRow[] matchingRows = new DataRow[found.Length];
				for (int i = 0; i < found.Length; i++)
				{
					lst.Add(found[i].Row);
				}

			}


			return lst.ToArray();
		}

	}
}
