using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Reflection;

namespace Agility.Web
{

	public interface IAgilityContentRepository<T> where T : AgilityContentItem
	{
		string LanguageCode { get; }
		string ContentReferenceName { get; }
		List<T> Items();
		List<T> Items(string rowFilter);
		List<T> Items(string rowFilter, string sort);
		List<T> Items(string rowFilter, string sort, int take, int skip);
		List<T> Items(string rowFilter, string sort, int take, int skip, out int totalCount);
		T Item(string rowFilter);

		T Item(int contentID);

		IList<T> SortByIDs(string ids);
		IList<T> SortByIDs(string ids, string filter);		

		IList<T> GetByIDs(string ids);
		IList<T> GetByIDs(IList<int> ids);		
		T GetByID(int contentID);
	}

	public class AgilityContentRepository<T> : IAgilityContentRepository<T> where T : AgilityContentItem
	{

		private string _contentReferenceName;
		private string _languageCode;
		private string _defaultSort = string.Empty;
		private string _defaultFilter = string.Empty;

		private DataView _contentItemsView = null;


		protected DataView ContentItemsView
		{
			get 
			{
				if (_contentItemsView == null)
				{
					_contentItemsView = Data.GetContentView(_contentReferenceName, _languageCode);					
				}
				return _contentItemsView;
			}
			
		}

		public string ContentReferenceName
		{
			get { return _contentReferenceName; }
			
		}
		
		public string LanguageCode
		{
			get { return _languageCode; }			
		}

		public AgilityContentRepository(string contentReferenceName) 
		{
			_contentReferenceName = contentReferenceName;
			_languageCode = AgilityContext.LanguageCode;
		}


		public AgilityContentRepository(string referenceName, string languageCode)
		{
			_contentReferenceName = referenceName;
			_languageCode = languageCode ?? AgilityContext.LanguageCode;
		}

		public AgilityContentRepository(string referenceName, string languageCode, string defaultSort, string defaultFilter)
		{
			_contentReferenceName = referenceName;
			_languageCode = languageCode;
			_defaultFilter = string.Format("{0}", defaultFilter).Trim();
			_defaultSort = string.Format("{0}", defaultSort).Trim();
		}

		public virtual List<T> Items()
		{
			return Items(_defaultFilter);			
		}

		public string DefaultSort
		{
			get
			{
				string defaultSort = _defaultSort;
				if (string.IsNullOrEmpty(defaultSort))
				{
					try
					{

						if (ContentItemsView != null && ContentItemsView.Table != null && ContentItemsView.Table.ExtendedProperties != null && ContentItemsView.Table.ExtendedProperties.ContainsKey("defaultSort"))
						{
							defaultSort = ContentItemsView.Table.ExtendedProperties["defaultSort"] as string;
						}
						else
						{
							defaultSort = null;
						}
					}
					catch (Exception ex)
					{
						defaultSort = null;
					}
				}

				return defaultSort;
			}
		}

		public virtual List<T> Items(string rowFilter)
		{

			return Items(rowFilter, null);
		}

		public virtual List<T> Items(string rowFilter, string sort)
		{
			return Items(rowFilter, sort, -1, -1);
			
		}

		public virtual List<T> Items(string rowFilter, string sort, int take, int skip)
		{
			int totalCount = -1;
			return Items(rowFilter, sort, take, skip, out totalCount);
		}

		public virtual List<T> Items(string rowFilter, string sort, int take, int skip, out int totalCount)
		{
			string testFilter = "ContentID = ";
			if (!String.IsNullOrEmpty(rowFilter) && rowFilter.IndexOf(testFilter, StringComparison.CurrentCultureIgnoreCase) == 0)
			{
				int contentID = -1;
				if (int.TryParse(rowFilter.Substring(testFilter.Length), out contentID))
				{
					var item = Item(contentID);
					if (item != null)
					{
						totalCount = 1;
						List<T> lst = new List<T>()
						{
							item
						};

						return lst;
					} else
					{
						totalCount = 0;
						List<T> lst = new List<T>();
					}
				}
			}

			totalCount = 0;
			if (ContentItemsView == null) return new List<T>(0);
            //ContentItemsView.RowFilter = rowFilter;
            //ContentItemsView.Sort = sort;

			if (string.IsNullOrEmpty(sort))
			{
				sort = DefaultSort;
			}

			DataRow[] items = null;
			if (ContentItemsView.Table.Columns.Count > 0)
			{

				items = ContentItemsView.Table.Select(rowFilter, sort, DataViewRowState.CurrentRows);
			} else
			{
				items = new DataRow[0];
			}
			
			totalCount = items.Length;

			return ConvertDataViewToObjects(items, skip, take);
		}


		public virtual T Item(string rowFilter)
		{
            //test for a "ContentID = X" filter...
            if (rowFilter != null)
            {
                string testFilter = "ContentID = ";
                if (rowFilter.IndexOf(testFilter, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    int contentID = -1;
                    if (int.TryParse(rowFilter.Substring(testFilter.Length), out contentID))
                    {
                        return Item(contentID);
                    }
                }
            }

			List<T> lst = Items(rowFilter);
			if (lst.Count > 0)
			{
				return lst[0];
			}
			return null;
		}

		public T Item(int contentID)
		{
			
			var dv = ContentItemsView;

			if (dv == null) return null;

			DataTable dt = dv.Table;
			if (dt != null)
			{



				//TODO: figure out the index...
				//DataTableIndex index = dt.ContentIDIndex;
				//DataRow[] rows = index.Find(contentID);
				DataRow[] rows = dt.Select($"ContentID = {contentID}");
				if (rows.Length > 0)
				{
					List<T> lst = ConvertDataViewToObjects(rows);
					if (lst.Count > 0) return lst[0];
				}
			}

			return null;

		}

		public IList<T> GetByIDs(string ids)
        {
			if (string.IsNullOrEmpty(ids)) return new List<T>();
            return GetByIDs(ToIntegers(ids, ','));
        }

        public IList<T> GetByIDs(IList<int> ids)
        {
            if (Items() == null || ids == null || ids.Count == 0) return new List<T>();
            string commaSeperatedIDs = string.Join(",", ids.Select(x => x.ToString()).ToArray());
            var items = Items(string.Format("ContentID in ({0})", commaSeperatedIDs));

            return items.OrderBy(i => ids.IndexOf(i.ContentID)).ToList();
        }

        public T GetByID(int contentID)
        {
            return Items() == null ? default(T) : Item(contentID);
        }

		public IList<T> SortByIDs(string ids)
		{
			if (string.IsNullOrEmpty(ids)) return Items();
			return SortAgilityContentItems(ToIntegers(ids, ','));
		}

		public IList<T> SortByIDs(string ids, string filter)
		{
			if (string.IsNullOrEmpty(ids)) return Items(filter);
			return SortAgilityContentItems(ToIntegers(ids, ','), filter);
		}


		protected virtual List<T> ConvertDataViewToObjects(DataRow[] view)
		{
			return ConvertDataViewToObjects(view, -1, -1);
		}

		protected virtual List<T> ConvertDataViewToObjects(DataRow [] rows, int skip, int take)
		{
			List<T> lst = null;
			if (take > 0)
			{
				lst = new List<T>(take);
			}
			else
			{
				lst = new List<T>(rows.Length);
			}

			Type type = typeof(T);
			
			ConstructorInfo constr = type.GetConstructor(System.Type.EmptyTypes);
			if (constr == null)
			{
				throw new ArgumentException("The provided type does not have a default constructor.");
			}

			if (skip < 1) skip = 0;

			for (int i = skip; i < rows.Length; i++) 
			{
				DataRow row = rows[i];

				T t = ConvertDataRowToObject(constr, row, LanguageCode, ContentReferenceName);
				
				lst.Add(t);

				if (take > 0 && lst.Count >= take)
				{
					//kick out if we've specified a take value
					break;
				}

			}
			return lst;
		}

		internal static T ConvertDataRowToObject(ConstructorInfo constr, DataRow row, string languageCode, string contentReferenceName)
		{
			T t = (T)constr.Invoke(new object[0]);

			t.DataRow = row;
			t.LanguageCode = languageCode;
			t.ReferenceName = contentReferenceName;

			int contentID = -1;
			int versionID = -1;

			if (!int.TryParse($"{row["ContentID"]}", out contentID)) contentID = -1;
			if (!int.TryParse($"{row["VersionID"]}", out versionID)) versionID = -1;

			t.ContentID = contentID;
			t.VersionID = versionID;
			t.State = row["State"] as string;
			if (!row.IsNull("PullDate"))
			{				
				t.PullDate = ConvertToDateTime(row["PullDate"]);
			}
			if (!row.IsNull("ReleaseDate"))
			{
				
				t.ReleaseDate = ConvertToDateTime(row["ReleaseDate"]);
			}

			if (!row.IsNull("ItemCreatedDate"))
			{
				var dt = ConvertToDateTime(row["ItemCreatedDate"]);
				t.CreatedDate = dt.HasValue  ? dt.Value : DateTime.MinValue;
			}

			if (!row.IsNull("CreatedDate"))
			{
				var dt = ConvertToDateTime(row["CreatedDate"]);
				t.ModifiedDate = dt.HasValue ? dt.Value : DateTime.MinValue;			
			}
			return t;
		}
		
		internal static DateTime? ConvertToDateTime(object o)
		{
			if (o is DateTime) return (DateTime)o;
			string s = o as string;
			if (string.IsNullOrWhiteSpace(s)) return null;

			DateTime dt = DateTime.MinValue;

			if (DateTime.TryParse(s, out dt)) return dt;
			return null;

		}
		

		private IList<T> SortAgilityContentItems(IList<int> ids)			
		{
			return SortAgilityContentItems(ids, string.Empty);
		}

		private IList<T> SortAgilityContentItems(IList<int> ids, string filter)			
		{
			
			var items = this.Items(string.Format("{0}", filter));

			if (items == null || ids == null || ids.Count() == 0)
			{
				return items;
			}

            List<T> lst = new List<T>(items.Count);
			foreach (int id in ids)
			{
				var item = items.FirstOrDefault(c => c.ContentID == id);
				if (item == null) continue;

				lst.Add(item);
			
			}


			var rng = items.Where(c => !ids.Any(i => i == c.ContentID));

			lst.AddRange(rng);

			return lst;

		
		}




		private static List<int> ToIntegers(string str, char delimiter)
		{
			List<int> result = new List<int>();

			if (string.IsNullOrEmpty(str)) return new List<int>();

			List<string> substrs = str.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries).ToList();

			foreach (var s in substrs)
			{
				int i;
				if (int.TryParse(s, out i))
				{
					result.Add(i);
				}
			}

			return result;
		}
    }
}
