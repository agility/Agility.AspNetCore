using System;
using System.Collections.Generic;
using System.Text;

namespace Agility.Web.Objects
{
	/// <summary>
	/// This object is a file that is used in the image or document libraries within Agility.
	/// </summary>
	public class Document : Agility.Web.AgilityContentServer.AgilityDocument
	{
		public string Filename;
		public string MimeType;
		public string Size;

		public byte[] Bytes
		{
			get
			{				
				return null;
			}
		}

		public string Path
		{
			get
			{
				
				return "";
			}
		}

	}
}
