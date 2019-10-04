using System.Collections.Specialized;

namespace Agility.Web.Extensions
{
	public static class NameValueCollectionExtensions
	{
		/// <summary>
		/// Returns the int value in the collection corresponding to the name provided.  If the value is less then 1, it returns -1.
		/// </summary>
		/// <param name="col"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static int ValueAsInt(this NameValueCollection col, string name)
		{
			if (col == null || col[name] == null) return -1;
			int id = -1;
			if (int.TryParse(col[name], out id) && id > 0)
			{
				return id;
			}
			else
			{
				return -1;
			}
		}

	}
}
