using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Web;
using System.Reflection;
using System.IO;

namespace Agility.Web.Configuration
{
	/// <summary>
	/// Defines the Trace and Debugging settings used for this application.
	/// </summary>
	public class TraceSettings
	{

		/// <summary>
		/// Gets/Sets whether to send emails when Exception level trace messages are written.
		/// </summary>		
		public bool EmailErrors { get; set; }
		
		/// <summary>
		/// Gets/Sets the TraceLevel for this application that will be used for logging. (Verbose, Info, Warning, Error, None)
		/// </summary>		
		public TraceLevel TraceLevel { get; set; }
		

		/// <summary>
		/// The collection of ErrorTraceType elements.
		/// </summary>	
		//HACK: public ErrorTraceCollection ErrorTraceTypes { get; set; }
		

		private static string _logFilePath = null;
		/// <summary>
		/// Gets/sets the path that will be used to store the log files for this application.
		/// </summary>		
		public string LogFilePath { get; set; }
		
		/// <summary>
		/// Gets/sets the ; delimited list of email addresses to send error emails to.
		/// </summary>		
		public string SendErrorsTo { get; set; }
		
		/// <summary>
		/// Gets/sets the email address to send error emails From.
		/// </summary>		
		public string SendErrorsFrom { get; set; }
		
	}
}
