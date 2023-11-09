using System;
using System.Collections.Generic;
using System.Text;
using Agility.Web.Enum;
using System.Web;
using Agility.Web.Configuration;

namespace Agility.Web.Objects
{
	public class AgilityPage
	{

		internal Agility.Web.AgilityContentServer.AgilityPage ServerPage = null;

		public int ID = -1;
		public string LanguageCode = string.Empty;
		public string Name = string.Empty;
		public string Title = string.Empty;
		public DateTime PullDate;
		public DateTime ReleaseDate;
		public ItemState State = ItemState.None;
		public string TemplatePath = string.Empty;
		public int TemplateID = -1;

		public string MetaTags;
		public string MetaKeyWords;
		public string MetaTagsRaw;
		public bool IncludeInStatsTracking;
		public string CustomAnalyticsScript;
		public bool ExcludeFromOutputCache;
		public bool RequiresAuthentication = false;
		public string RedirectURL;
		public bool IsPublished = false;
        
        public string DynamicPageContentViewReferenceName;

        private string _URL = "";
        public string URL
        {
            get
            {
                if (_URL == "")
                {
                    var sitemap = new AgilitySiteMap();
                    var node = sitemap.FindSiteMapNodeFromKey(ID.ToString());

                    if (node != null)
                    {
                        _URL = node.Url;
                    }
                }
                return _URL;
            }
        }



		private Agility.Web.Objects.ContentSection[] _contentSections;

		public Agility.Web.Objects.ContentSection[] ContentSections
		{
			get {
				if (_contentSections == null) _contentSections = new ContentSection[0];
				return _contentSections; 
			}
			set { _contentSections = value; }
		}

		/// <summary>
		/// If this page or it's parent is a dynamic page, this will allow you to access that content.
		/// </summary>
		public DynamicPageItem DynamicPageItem { get; set; }

	}
}
