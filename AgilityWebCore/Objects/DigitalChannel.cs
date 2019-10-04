using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agility.Web.AgilityContentServer;

namespace Agility.Web.Objects
{
	public class DigitalChannel
	{
		public DigitalChannel()
		{

		}

		internal DigitalChannel(Agility.Web.AgilityContentServer.AgilityDigitalChannel channel)
		{
			this.ID = channel.ID;
			this.ReferenceName = channel.ReferenceName;
			this.DisplayName = channel.DisplayName;
			if (channel.DigitalChannelDomains != null)
			{

				var domains = from d in channel.DigitalChannelDomains
							  select ChannelDomain.Parse(d);


				_domains.AddRange(domains);
			}
		}

		public string ReferenceName { get; set; }
		public string DisplayName { get; set; }
		public int ID { get; set; }

		List<ChannelDomain> _domains = new List<ChannelDomain>();
		public List<ChannelDomain> Domains
		{
			get
			{
				return _domains;
			}
		}
	}

	public class ChannelDomain
	{

		internal static ChannelDomain Parse(AgilityDigitalChannelDomain dc)
		{
			ChannelDomain domain = new ChannelDomain()
			{
				ID = dc.DigitalChannelDomainID,
				DefaultPath = dc.DefaultPath,
				URL = dc.DomainUrl,
				UserAgents = new List<string>(),
				DefaultLanguage = dc.XDefaultLanguage,
				ForceDefaultLanguageToThisDomain = dc.XForceDefaultLanguageToThisDomain,
				ForceUserAgentsToThisChannel = dc.ForceUserAgentsToThisChannel

			};

			if (dc.UserAgentFilters != null)
			{
				domain.UserAgents.AddRange(dc.UserAgentFilters);
			}

			return domain;
		}

		public int ID { get; set; }
		public string DefaultPath { get; set; }
		public string URL { get; set; }
		public string DefaultLanguage { get; set; }
		public bool ForceDefaultLanguageToThisDomain { get; set; }
		public bool ForceUserAgentsToThisChannel { get; set; }
		public List<string> UserAgents { get; set; }


	}

	
}
