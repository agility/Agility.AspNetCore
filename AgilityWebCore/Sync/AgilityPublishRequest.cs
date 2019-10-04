using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace Agility.Web.Sync
{
	/// <summary>
	/// Class referenced via the ContentPublishService to tell the Website which website and domain to pull sync content with.
	/// </summary>
	/// <remarks>
	/// This class instance must contain a valid SecurityKey value in order for the Publish to move forward.
	/// </remarks>
	public class AgilityPublishRequest
	{
		public string WebsiteDomain { get; set; }

		public string WebsiteName { get; set; }

		public string SecurityKey { get; set; }

        public AgilityPublishRequest() { }
	}

}
