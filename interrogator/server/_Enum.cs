using System;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	/// <summary>
	/// Represents the types of response data that can be returned from the Command Manager.
	/// </summary>
	public enum PacketType : byte
	{
		CommandResponse = 0,
		SensorData = 1,
		ArchiveData = 2,
		EventData = 3
	}

	/// <summary>
	/// Represents the exit status of an issued command. A non-zero status indicates an error.
	/// </summary>
	public enum CommandExitStatus : byte
	{
		Success = 0,
		Failed = 1,
		ErrorProcessing = 2,
		AccessDenied = 3,
		InvalidCommand = 4,
		InvalidNumberArguments = 5,
		InvalidArgument = 6
	}
}

