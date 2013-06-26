using System;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	public interface ICommandServer
	{
		bool Enabled { get; }

		bool Start();
		void Stop();

		void AddCommand( string name, string description, bool passwordRequired, bool hideHelpText,
			ServerCommandDelegate command );
		void RemoveCommand( string commandText );
	}
}

