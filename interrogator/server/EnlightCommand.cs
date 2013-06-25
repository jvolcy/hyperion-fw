using System;
using System.Collections.Generic;
using System.Text;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	public delegate CommandStatus EnlightCommandDelegate( string[] commandFields, out byte[] responseBytes );

	public class EnlightCommand
	{
		#region -- Instance Attributes --

		private EnlightCommandDelegate mCommandDelegate;

		private string mCommandText;
		private string mDescription;

		private bool mPasswordRequired;
		private bool mHideHelpText;

		#endregion


		#region -- Constructors --

		/// <summary>
		/// Create a new ENLIGHT command.
		/// </summary>
		/// <param name="commandText">The string necessary to invoke the command through the command interface.</param>
		/// <param name="description">A short phrase that explains what actions the command performs or what data it returns.</param>
		/// <param name="passwordRequired">true to only allow execution after the priveledged mode password has been entered; false to make
		/// available always.</param>
		/// <param name="hideHelpText">true to hide help text in unpriveledged mode; false to display it (usefule for things like #SET_PASSWORD).</param>
		/// <param name="commandDelegate">The method that executes the command.</param>
		public EnlightCommand( string commandText, string description, bool passwordRequired, bool hideHelpText,
			EnlightCommandDelegate commandDelegate )
		{
			this.mCommandText = commandText;
			this.mDescription = description;
			this.mPasswordRequired = passwordRequired;
			this.mHideHelpText = hideHelpText;

			this.mCommandDelegate = commandDelegate;
		}

		#endregion


		#region -- Internal Properties --

		/// <summary>
		/// Get the string necessary to invoke the command through the command interface.
		/// </summary>
		internal string CommandText
		{
			get { return this.mCommandText; }
		}

		/// <summary>
		/// Get the short phrase that explains what actions the command performs or what data it returns.
		/// </summary>
		internal string Description
		{
			get { return this.mDescription; }
		}

		/// <summary>
		/// Get a value that indicates if priveledged execute mode is required to execute the command.
		/// </summary>
		internal bool PasswordRequired
		{
			get { return this.mPasswordRequired; }
		}

		/// <summary>
		/// Get a value that indicates if the commands Help text is displayed in unpriveledged mode.
		/// </summary>
		internal bool HideHelpText
		{
			get { return this.mHideHelpText; }
		}

		/// <summary>
		/// Get the method that executes the command.
		/// </summary>
		internal EnlightCommandDelegate CommandDelegate
		{
			get { return this.mCommandDelegate; }
		}

		#endregion
	}
}
