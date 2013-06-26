using System;
using System.Collections.Generic;
using System.Text;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	public delegate CommandExitStatus ServerCommandDelegate( string[] commandFields, out byte[] responseBytes );

	internal class ServerCommand
	{
		#region -- Instance Attributes --

		private ServerCommandDelegate mCommandDelegate;

		private string _Name;
		private string _Description;

		private bool _PasswordRequired;
		private bool _HideHelpText;

		#endregion


		#region -- Constructors --

		/// <summary>
		/// Create a new server command.
		/// </summary>
		/// <param name="name">The string to invoke the command.</param>
		/// <param name="description">A short phrase that explains what actions the command performs or what data it returns.</param>
		/// <param name="passwordRequired">true to only allow execution after the priveledged mode password has been entered; false to make
		/// available always.</param>
		/// <param name="hideHelpText">true to hide help text in unpriveledged mode; false to display it (usefule for things like #SET_PASSWORD).</param>
		/// <param name="commandDelegate">The method that executes the command.</param>
		internal ServerCommand( string name, string description, bool passwordRequired, bool hideHelpText,
			ServerCommandDelegate commandDelegate )
		{
			this._Name = name;
			this._Description = description;
			this._PasswordRequired = passwordRequired;
			this._HideHelpText = hideHelpText;

			this.mCommandDelegate = commandDelegate;
		}

		#endregion


		#region -- Internal Properties --

		/// <summary>
		/// Get the string necessary to invoke the command.
		/// </summary>
		internal string Name
		{
			get { return this._Name; }
		}

		/// <summary>
		/// Get the short phrase that explains what actions the command performs and what (if any) data it returns.
		/// </summary>
		internal string Description
		{
			get { return this._Description; }
		}

		/// <summary>
		/// Get a value that indicates if priveledged execute mode is required to execute the command.
		/// </summary>
		internal bool PasswordRequired
		{
			get { return this._PasswordRequired; }
		}

		/// <summary>
		/// Get a value that indicates if the commands Help text is displayed in unpriveledged mode.
		/// </summary>
		internal bool HideHelpText
		{
			get { return this._HideHelpText; }
		}

		/// <summary>
		/// Get the method that executes the command.
		/// </summary>
		internal ServerCommandDelegate CommandDelegate
		{
			get { return this.mCommandDelegate; }
		}

		#endregion
	}
}
