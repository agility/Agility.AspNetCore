using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Runtime.Serialization;


namespace Agility.Web.Exceptions
{
	/// <summary>
	/// Base class for all Exceptions that are explicitly thrown from the Agility.Web assembly.
	/// </summary>
	public class AgilityException : ApplicationException
	{
		bool _traceAsWarning = false;

		public bool TraceAsWarning
		{
			get { return _traceAsWarning; }
			set { _traceAsWarning = value; }
		}

		public AgilityException(string message) : base(message) { }

		/// <summary>
		/// Creates an AgilityException that will be traced as a warning if traceAsWarning is true.
		/// </summary>
		/// <param name="message"></param>
		/// <param name="traceAsWarning"></param>
		public AgilityException(string message, bool traceAsWarning) : base(message) {
			_traceAsWarning = traceAsWarning;
		}
		public AgilityException(string message, Exception innerException) : base(message, innerException) { }
	}

	
}
