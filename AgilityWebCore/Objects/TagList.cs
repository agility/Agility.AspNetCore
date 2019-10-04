using System.Data;


namespace Agility.Web.Objects
{
	public class TagList
	{
		
		private string _languageCode = string.Empty;
		internal DataSet _dsTags = null;

		/// <summary>
		/// The language code that this TagList is to be used on.
		/// </summary>
		public string LanguageCode
		{
			get { return _languageCode; }
			set { _languageCode = value; }
		}

		/// <summary>
		/// Gets the datatable of Tags.
		/// </summary>
		public DataTable Tags
		{
			get
			{
				if (_dsTags == null) return null;
				return _dsTags.Tables["Tags"];

			}
		}

		/// <summary>
		/// Gets the global datatable of stats for Tags.
		/// </summary>
		public DataTable TagStats
		{
			get
			{
				if (_dsTags == null) return null;
				return _dsTags.Tables["TagStats"];

			}
		}

		/// <summary>
		/// Gets the global datatable of stats for Tags on every ContentView.
		/// </summary>
		public DataTable TagContentStats
		{
			get
			{
				if (_dsTags == null) return null;
				return _dsTags.Tables["TagContentStats"];

			}
		}
	}
}
