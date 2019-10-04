using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Agility.Web.Requirements
{
    internal class CorrectWebsiteRequirement: IAuthorizationRequirement
    {
        public string WebsiteName { get; set; }
        public string SecurityKey { get; set; }

        public CorrectWebsiteRequirement(string websiteName, string securityKey)
        {
            this.WebsiteName = websiteName;
            this.SecurityKey = securityKey;
        }
    }
}
