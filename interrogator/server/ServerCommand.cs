using System;
using System.Collections.Generic;
using System.Text;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	public delegate CommandExitStatus ServerCommandDelegate( string[] commandFields, out byte[] responseBytes );

	internal class ServerCommand
	{
		#region -- Instance Attributes --

		private ServerCommandDelegate _commandDelegate;

		private string _name;
		private string _description;

		private bool _passwordRequired;
		private bool _hideHelpText;

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
			this._name = name;
			this._description = description;
			this._passwordRequired = passwordRequired;
			this._hideHelpText = hideHelpText;

			this._commandDelegate = commandDelegate;
		}

		#endregion


		#region -- Internal Properties --

		/// <summary>
		/// Get the string necessary to invoke the command.
		/// </summary>
		internal string Name
		{
			get { return this._name; }
		}

		/// <summary>
		/// Get the short phrase that explains what actions the command performs and what (if any) data it returns.
		/// </summary>
		internal string Description
		{
			get { return this._description; }
		}

		/// <summary>
		/// Get a value that indicates if priveledged execute mode is required to execute the command.
		/// </summary>
		internal bool PasswordRequired
		{
			get { return this._passwordRequired; }
		}

		/// <summary>
		/// Get a value that indicates if the commands Help text is displayed in unpriveledged mode.
		/// </summary>
		internal bool HideHelpText
		{
			get { return this._hideHelpText; }
		}

		/// <summary>
		/// Get the method that executes the command.
		/// </summary>
		internal ServerCommandDelegate CommandDelegate
		{
			get { return this._commandDelegate; }
		}

		#endregion
	}
}
