using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Http.Extensions;

using Agility.Web;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Edentity.Shared
{
	/// <summary>
	/// This is an abstract class used to manipulate the Url including query strings.
	/// </summary>
	internal class Url
	{

		/// <summary>
		/// This adds the new Query Strings to the current Url
		/// </summary>
		/// <param name="query"></param>
		/// <returns></returns>
		public string AppendQueryString(string query)
		{
			var context = AgilityContext.HttpContext;

			//This string tokenizes with & as the token.
			string[] newQueries = query.Split('&');
			string fullURL = "";

			var nvc =  ParseQueryString(context.Request.QueryString.Value);
				
			fullURL = AppendQuery(nvc, newQueries, null);

			if (fullURL == "")
			{
				return context.Request.GetEncodedUrl();
			}
			else
			{
				return context.Request.Path.Value + "?" +  fullURL;
			}
		}

		/// <summary>
		/// This adds the new Query Strings to a supplied root path
		/// </summary>
		/// <param name="rootPath"></param>
		/// <param name="newQueryStrings"></param>
		/// <returns></returns>
		public string AppendQueryString(string rootPath, string newQueryStrings)
		{
			//This breaks up the query strings and the path
			string[] splitPath = rootPath.Split('?');
			if (splitPath.Length > 1)
			{
				NameValueCollection nvc = ParseQueryString(splitPath[1]);

				//This string tokenizes with & as the token.
				string[] newQueries = newQueryStrings.Split('&');
				string fullURL = "";

				fullURL = AppendQuery(nvc, newQueries, null);

				if (fullURL == "")
				{
					return rootPath;
				}
				else
				{
					return splitPath[0] + "?" + fullURL;
				}
			}
			else
			{
				return rootPath + "?" + newQueryStrings;
			}

		}

		/// <summary>
		/// This removes the query string from the specified rootpath.
		/// </summary>
		/// <param name="removeQueryStrings"></param>
		/// <returns></returns>
		public string RemoveQueryString(string removeQueryStrings)
		{
			var context = AgilityContext.HttpContext;

			string rootPath = context.Request.GetEncodedUrl();			

			//This breaks up the query strings and the path
			
			if (! string.IsNullOrEmpty(context.Request.QueryString.Value))
			{
				NameValueCollection nvc = ParseQueryString(rootPath);

				//This string tokenizes with & as the token.
				string[] removeQueries = removeQueryStrings.Split('&');
				string fullURL = "";

				fullURL = AppendQuery(nvc, null, removeQueries);

				if (fullURL == "")
				{
					return context.Request.Path.Value;
				}
				else
				{
					return context.Request.Path.Value + "?" + fullURL;
				}
			}
			else
			{
				return rootPath;
			}

		}
		/// <summary>
		/// This removes the query string from the specified rootpath.
		/// </summary>
		/// <param name="rootPath"></param>
		/// <param name="removeQueryStrings"></param>
		/// <returns></returns>
		public string RemoveQueryString(string rootPath, string removeQueryStrings)
		{
			//This breaks up the query strings and the path
			string[] splitPath = rootPath.Split('?');
			if (splitPath.Length > 1)
			{
				NameValueCollection nvc = ParseQueryString(splitPath[1]);


				//This string tokenizes with & as the token.
				string[] removeQueries = removeQueryStrings.Split('&');
				string fullURL = "";

				fullURL = AppendQuery(nvc, null, removeQueries);

				if (fullURL == "")
				{
					return splitPath[0];
				}
				else
				{
					return splitPath[0] + "?" + fullURL;
				}
			}
			else
			{
				return rootPath;
			}

		}

		/// <summary>
		/// This adds the new Query Strings and removes the Query strings supplied.  If the query string being
		/// added already exists it replaces it.
		/// </summary>
		/// <param name="newQueryStrings"></param>
		/// <param name="removeQueryStrings"></param>
		/// <returns>The modified query string</returns>
		public string ModifyQueryString(string newQueryStrings, string removeQueryStrings)
		{
			var context = AgilityContext.HttpContext;

			//This string tokenizes with & as the token.
			string[] newQueries = newQueryStrings.Split('&');
			string[] removeQueries = removeQueryStrings.Split('&');

			string fullURL = "";

			NameValueCollection nvc = ParseQueryString(context.Request.QueryString.Value);

			fullURL = AppendQuery(nvc, newQueries, removeQueries);

			if (fullURL == "")
			{
				return context.Request.Path.Value;
			}
			else
			{
				return context.Request.Path.Value + "?" + fullURL;
			}
		}

		/// <summary>
		/// This adds the new Query Strings and removes the Query strings supplied.  If the query string being
		/// added already exists it replaces it.
		/// </summary>
		/// <param name="rootPath"></param>
		/// <param name="newQueryStrings"></param>
		/// <param name="removeQueryStrings"></param>
		/// <returns>The modified query string</returns>
		public string ModifyQueryString(string rootPath, string newQueryStrings, string removeQueryStrings)
		{
			var context = AgilityContext.HttpContext;
			

			string[] splitPath = rootPath.Split('?');


			//This string tokenizes with & as the token.
			string[] newQueries = newQueryStrings.Split('&');
			string[] removeQueries = removeQueryStrings.Split('&');

			string fullURL = "";
			if (splitPath.Length > 1)
			{

				fullURL = AppendQuery(ParseQueryString(rootPath), newQueries, removeQueries);
			}
			else
			{
				fullURL = AppendQuery(new NameValueCollection(), newQueries, removeQueries);
			}

			if (fullURL == "")
			{
				return splitPath[0];
			}
			else
			{
				return splitPath[0] + "?" + fullURL;
			}


		}

		/// <summary>
		/// This takes the query string as a string and converts it to a Name Value Collection
		/// </summary>
		/// <param name="queryString"></param>
		/// <returns></returns>
		public NameValueCollection ParseQueryString(string queryString)
		{
			//Trim off the beginning question mark.
			if (queryString.IndexOf("?") != -1)
			{
				queryString = queryString.Substring(queryString.IndexOf('?') + 1);
			}

			StringTokenizer st = new StringTokenizer(queryString, "&");

			NameValueCollection retCollection = new NameValueCollection(st.CountTokens());
			string[] nameValue;
			while (st.HasMoreTokens())
			{
				nameValue = st.NextToken().Split('=');
				if (nameValue.Length > 0 && nameValue[0] != "")
				{
					//This is the care where the value for the key is blank.
					if (nameValue.Length > 1)
					{
						retCollection.Add(nameValue[0], nameValue[1]);
					}
					else
					{
						retCollection.Add(nameValue[0], "");
					}
				}

			}
			return retCollection;
		}

		private string AppendQuery(NameValueCollection currentQuery, string[] newQueries, string[] removeQueries)
		{

			//go through the current query and re-encode the values
			NameValueCollection queryCollection = new NameValueCollection(currentQuery);

			//foreach (string key  in currentQuery.AllKeys) 
			//{
			//    if (! string.IsNullOrEmpty(currentQuery[key])) 
			//    {
			//        queryCollection.Add(key, HttpUtility.UrlEncode(currentQuery[key]));					
			//    }
			//}

			

			if (newQueries != null)
			{
				string[] nameValue;
				string keyValue = "";
				foreach (string query in newQueries)
				{
					nameValue = query.Split('=');
					if (nameValue.Length > 0)
					{
						//This is for the cause that there is a key but no value.
						if (nameValue.Length < 2)
						{
							keyValue = "";
						}
						else
						{
							keyValue = nameValue[1];
						}
						if (queryCollection[nameValue[0]] != null)
						{
							//If it exists, just modify
							queryCollection[nameValue[0]] = keyValue;
						}
						else
						{
							//Insert:
							queryCollection.Add(nameValue[0], keyValue);
						}
					}
				}
			}
			if (removeQueries != null)
			{
				//Remove any in the removeQuery string array
				foreach (string query in removeQueries)
				{
					queryCollection.Remove(query);
				}
			}
			string retQuery = "";
			foreach (string query in queryCollection)
			{
				if (query != null && query != "")
				{
					if (query.ToLower() == "pageurl") continue;
					if (retQuery == "")
					{
						//retQuery = query + "=" +  HttpUtility.UrlEncode(queryCollection[query]);
						retQuery = query + "=" + queryCollection[query];
					}
					else
					{
						//retQuery += "&" + query + "=" + HttpUtility.UrlEncode(queryCollection[query]);
						retQuery += "&" + query + "=" + queryCollection[query];
					}
				}
			}
			return retQuery;
		}


		/// <summary>
		/// Replace an instance of href="~/ or src="~/ in a string with a resolved url.
		/// </summary>
		/// <param name="html"></param>
		/// <returns></returns>
		public string ResolveTildaUrlsInHtml(string html)
		{
			var context = AgilityContext.HttpContext;

			if (context == null) return html;

			html = html.Replace("http://manager1201.agilitycms.com/Dialogs/~/", "~/");
			html = html.Replace("http://manager1201.agilitycms.com/Dialogs/%7E/", "~/");


			string[] replacments = { "~/", "%7E/" };
			string appPath = "/";  //Assume root web app
			if (!appPath.EndsWith("/")) appPath = string.Format("{0}/", appPath);
			

			foreach (string replacement in replacments)
			{

				string urlPrefix = appPath;
				//if (AgilityContext.IsUsingLanguageModule && replacement.IndexOf(".aspx", StringComparison.InvariantCultureIgnoreCase) != -1)
				//{
				//	urlPrefix = string.Format("{0}{1}/", urlPrefix, AgilityContext.LanguageCode);
				//}


				

				string href1 = "href=\"" + urlPrefix;
				string href2 = "href='" + urlPrefix;

				string src1 = "src=\"" + urlPrefix;
				string src2 = "src='" + urlPrefix;

				if (html.IndexOf("href=", StringComparison.CurrentCultureIgnoreCase) != -1)
				{
					html = html.Replace("href=\"" + replacement, href1);
					html = html.Replace("HREF=\"" + replacement, href1);
					html = html.Replace("Href=\"" + replacement, href1);
					html = html.Replace("href='" + replacement, href2);
					html = html.Replace("HREF='" + replacement, href2);
					html = html.Replace("Href='" + replacement, href2);
				}


				if (html.IndexOf("src=", StringComparison.CurrentCultureIgnoreCase) != -1)
				{
					html = html.Replace("src=\"" + replacement, src1);
					html = html.Replace("SRC=\"" + replacement, src1);
					html = html.Replace("Src=\"" + replacement, src1);
					html = html.Replace("src='" + replacement, src2);
					html = html.Replace("SRC='" + replacement, src2);
					html = html.Replace("Src='" + replacement, src2);
				}


				if (html.StartsWith("~/"))
				{
					html = urlPrefix + html.Substring(2);
				}
			}

			if (html.IndexOf("target=", StringComparison.CurrentCultureIgnoreCase) != -1)
			{
				html = html.Replace("target=\"\"", "target=\"_self\"");
				html = html.Replace("Target=\"\"", "Target=\"_self\"");
				html = html.Replace("target=\'\'", "target=\'_self\'");
				html = html.Replace("Target=\'\'", "Target=\'_self\'");
			}

			if (string.IsNullOrEmpty(AgilityContext.FeaturedImageUrl) 
				&& (html.IndexOf("data-social-featured=\"true\"", StringComparison.CurrentCultureIgnoreCase) != -1
					|| html.IndexOf("data-social-featured='true'", StringComparison.CurrentCultureIgnoreCase) != -1)
				)
			{
				//featured image...
				int index = html.IndexOf("data-social-featured=\"true\"", StringComparison.CurrentCultureIgnoreCase);
				string quote = "\"";
				if (index == -1)
				{
					index = html.IndexOf("data-social-featured='true'", StringComparison.CurrentCultureIgnoreCase);
					quote = "'";
				}
					
				if (index > -1)
				{
						 

					int imgIndex = html.LastIndexOf("<img ", index);
					int imgIndex2 = html.IndexOf(">", index);
					if (imgIndex > -1 && imgIndex2 > -1)
					{
						int charCount = imgIndex2 - imgIndex;
						int srcIndex = html.IndexOf("src=" + quote, imgIndex, charCount, StringComparison.CurrentCultureIgnoreCase);
						if (srcIndex > -1)
						{
							srcIndex += 5;

							int srcIndex2 = html.IndexOf(quote, srcIndex);
							if (srcIndex2 > -1 && srcIndex2 > srcIndex)
							{
								string src = html.Substring(srcIndex, srcIndex2 - srcIndex);
								if (!string.IsNullOrEmpty(src))
								{
									if (src.IndexOf("://") == -1 && src.StartsWith("/"))
									{
										string host = context.Request.Host.Host;
										src = string.Format("{0}{1}", host, src);
									}

									AgilityContext.FeaturedImageUrl = src;
								}
							}

						}
					}
				}
				

			}

			return html;

		}

		/// <summary>
		/// Removes all non-alpha numeric characters from a string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public string RemoveSpecialCharacters(string str)
		{
			string s = Regex.Replace(str, @"[^\w\.\-@-]", "");
			
			if (s.IndexOf(".") != -1)
			{
				string ext = s.Substring(s.LastIndexOf("."));
				string pre = s.Substring(0, s.LastIndexOf("."));
				pre = pre.Replace(".", string.Empty);
				return pre + ext;
			}
			return s;
		}


        /// <summary>
        /// http://en.wikipedia.org/wiki/Diacritic
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public string RemoveDiacritics(string s)
        {

            String normalizedString = s.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < normalizedString.Length; i++)
            {
                Char c = normalizedString[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    stringBuilder.Append(c);
            }

            return stringBuilder.ToString();

        }
	}
}