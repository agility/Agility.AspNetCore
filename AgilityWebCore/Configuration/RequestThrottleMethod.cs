using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Agility.Web.Configuration
{
    public enum RequestThrottleIdentifier
    {
        None = 0,
        IpAddress = 1,
        UserAgent = 2
    }
}
