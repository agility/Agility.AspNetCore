using System;
using System.Collections.Generic;
using System.Text;

namespace Agility.Web.Configuration
{
	
	public enum TraceLevel
	{
		/// <summary>
		/// Output no tracing and debugging messages.
		/// </summary>
		Off = 0,
		
		/// <summary>
		/// Output error-handling messages.
		/// </summary>
		Error = 1,
		
		/// <summary>
		/// Output warnings and error-handling messages.
		/// </summary>
		Warning = 2,
		
		/// <summary>
		/// Output informational messages, warnings, and error-handling messages.
		/// </summary>
		Info = 3,
		
		/// <summary>
		/// Output all debugging and tracing messages.
		/// </summary>
		Verbose = 4,
	}
}
