using System;
using System.Collections.Generic;
using System.Text;

namespace Agility.Web.Objects
{
	/// <summary>
	/// Represents a content section on an AgilityPage object.
	/// </summary>
	public class ContentSection
	{
		private string _Name = "";

		/// <summary>
		/// The display name of the Module Zone for this ContentSection.
		/// </summary>
		public string Name
		{
			get { return _Name; }
			set { _Name = value; }
		}
		private string _ContentReferenceName = "";

		/// <summary>
		/// The Reference Name of the AgilityContent object associated with this ContentSection.
		/// </summary>
		public string ContentReferenceName
		{
			get { return _ContentReferenceName; }
			set { _ContentReferenceName = value; }
		}
		private string _SortExpression = "";

		/// <summary>
		/// SortExpression - not used.
		/// </summary>
		public string SortExpression
		{
			get { return _SortExpression; }
			set { _SortExpression = value; }
		}
		private string _FilterExpression = "";

		/// <summary>
		/// FilterExpression - not used.
		/// </summary>
		public string FilterExpression
		{
			get { return _FilterExpression; }
			set { _FilterExpression = value; }
		}
		private string _TemplateMarkup = "";

		 
		/// <summary>
		/// TemplateMarkup - Not used.
		/// </summary>
		public string TemplateMarkup
		{
			get { return _TemplateMarkup; }
			set { _TemplateMarkup = value; }
		}
		private string _UserControlPath = "";

		/// <summary>
		/// UserControl Path - Not Used
		/// </summary>
		public string UserControlPath
		{
			get { return _UserControlPath; }
			set { _UserControlPath = value; }
		}
		private int _ContentSectionOrder;

		/// <summary>
		/// The sort order of this Content Section in the Page.
		/// </summary>
		public int ContentSectionOrder
		{
			get { return _ContentSectionOrder; }
			set { _ContentSectionOrder = value; }
		}

		public int ExperimentID { get; set; }

		private int _ModuleOrder;

		/// <summary>
		/// The sort order of this Module within the Content Zone.
		/// </summary>
		public int ModuleOrder
		{
			get { return _ModuleOrder; }
			set { _ModuleOrder = value; }
		}
		private int _ModuleID;

		/// <summary>
		/// The ModuleID (content definition id) of this module.
		/// </summary>
		public int ModuleID
		{
			get { return _ModuleID; }
			set { _ModuleID = value; }
		}

		private AgilityContent _moduleContent;

		/// <summary>
		/// Gets the AgilityContent object associated with ContentReferenceName of this object. 
		/// </summary>
		public AgilityContent ModuleContent
		{
			get
			{
				if (string.IsNullOrEmpty(ContentReferenceName)) return null;

				if (_moduleContent == null)
				{
					_moduleContent = Data.GetContent(ContentReferenceName);
				}
				return _moduleContent;
			}
		}
		 
	}
}
