using System;
using System.Collections.Generic;
using System.Text;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	/// <summary>
	/// The MoiException class provides the base class for all custom Exceptions within 
	/// Micron Optics coding framework.
	/// </summary>
	public class MoiException : Exception
	{
		#region -- Constructors --

		/// <summary>
		/// Initializes a new instance of the MoiException class.
		/// </summary>
		public  MoiException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the MoiException class with a specified error message.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		public MoiException( string message ) : base( message )
		{
		}

		/// <summary>
		/// Initializes a new instance of the MoiException class with a specified error message and a 
		/// reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception. If
		/// the innerException parameter is not a null reference (Nothing in Visual Basic), the current
		/// exception is raised in a catch block that handles the inner exception. </param>
		public MoiException( string message, Exception innerException ) : base( message, innerException )
		{
		}
	
		#endregion


		#region -- Static Methods --

		/// <summary>
		/// Get a concatenated list of excpetion messages from a chain of exceptions/inner exceptions.
		/// </summary>
		/// <param name="ex"></param>
		/// <returns>A single message that contains the recursed list of messages from each inner exception.</returns>
		public static string GetAllMessages( Exception ex )
		{
			// Display all messages including inner exceptions
			string message = string.Empty;

			while ( ex != null )
			{
				// Append message
				message += ex.Message;

				// Append => if more messages
				if ( ( ex = ex.InnerException ) != null )
				{
					message += " => ";
				}
			}

			return message;
		}

		#endregion
	}
}
