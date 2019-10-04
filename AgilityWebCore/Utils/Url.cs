using System;
using System.Web;


using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace Agility.Web.Util
{
	
	public class Url
	{
		/// <summary>
		/// This adds the new Query Strings to the current Url
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public static string AppendQueryString(string query)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.AppendQueryString(query);

		}

		/// <summary>
		/// This adds the new Query Strings to a supplied root path
		/// </summary>
		/// <param name="rootPath"></param>
		/// <param name="newQueryStrings"></param>
		/// <returns></returns>
		public static string AppendQueryString(string rootPath, string newQueryStrings)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.AppendQueryString( rootPath,  newQueryStrings);
		}

		/// <summary>
		/// This removes the query string from the specified rootpath.
		/// </summary>
		/// <param name="removeQueryStrings"></param>
		/// <returns></returns>
		public static string RemoveQueryString(string removeQueryStrings)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.RemoveQueryString(removeQueryStrings);


		}
		/// <summary>
		/// This removes the query string from the specified rootpath.
		/// </summary>
		/// <param name="rootPath"></param>
		/// <param name="removeQueryStrings"></param>
		/// <returns></returns>
		public static string RemoveQueryString(string rootPath, string removeQueryStrings)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.RemoveQueryString(rootPath, removeQueryStrings);

		}

		/// <summary>
		/// This adds the new Query Strings and removes the Query strings supplied.  If the query string being
		/// added already exists it replaces it.
		/// </summary>
		/// <param name="newQueryStrings"></param>
		/// <param name="removeQueryStrings"></param>
		/// <returns>The modified query string</returns>
		public static string ModifyQueryString(string newQueryStrings, string removeQueryStrings)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.ModifyQueryString(newQueryStrings, removeQueryStrings);
		}

		/// <summary>
		/// This adds the new Query Strings and removes the Query strings supplied.  If the query string being
		/// added already exists it replaces it.
		/// </summary>
		/// <param name="rootPath"></param>
		/// <param name="newQueryStrings"></param>
		/// <param name="removeQueryStrings"></param>
		/// <returns>The modified query string</returns>
		public static string ModifyQueryString(string rootPath, string newQueryStrings, string removeQueryStrings)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.ModifyQueryString(rootPath, newQueryStrings, removeQueryStrings);
		}

		/// <summary>
		/// This takes the query string as a string and converts it to a Name Value Collection
		/// </summary>
		/// <param name="queryString"></param>
		/// <returns></returns>
		public static NameValueCollection ParseQueryString(string queryString)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.ParseQueryString(queryString);
		}

		/// <summary>
		/// Replace an instance of href="~/ or src="~/ in a string with a resolved url.
		/// </summary>
		/// <param name="html"></param>
		/// <returns></returns>
		public static string ResolveTildaUrlsInHtml(string html)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.ResolveTildaUrlsInHtml(html);

		}

		/// <summary>
		/// Removes all non-alpha numeric characters from a string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string RemoveSpecialCharacters(string str)
		{
			Edentity.Shared.Url url = new Edentity.Shared.Url();
			return url.RemoveSpecialCharacters(str);
		}

		
	}

	

}
