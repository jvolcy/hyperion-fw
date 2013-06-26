using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;

using MicronOptics;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	/// <summary>
	/// The TcpCommandServer provides the infrastructue for receiving, processing, and responding to
	/// incoming MOI-like commands (#GoBleepYourself) over a TCP connection.
	/// </summary>
	public class TcpCommandServer : ICommandServer
	{
		#region -- Constants --

		public static readonly string True = "1";
		public static readonly string False = "0";

		// Connnection
		private const int MaximumNumberOfConnections = 5;

		private const int DefaultTcpClientSendTimeoutInMilliseconds = 1000;
		private const int DefaultTcpClientSendBufferSizeInBytes = 128 * 1024;	// 128 kbytes

		public const int PortDefault = 1853;

		public const int PortMiniumum = 1000;
		public const int PortMaximum = 65535;

		// Commands
		private const int CommandMaximumSizeInBytes = 2048;
		private const char CommandTermination = '\n';

		private readonly char[] CommandFieldDelimiters = new char[] { ' ', ',', '\n' };

		private const int CommandResponseLengthFieldLengthInBytes = 4;			// 32-bit unsigned integer
		private const int CommandResponseDataTypeFieldLengthInBytes = 1;		// 8-bit unsigned integer
		private const int CommandResponseStatusFieldLengthInBytes = 1;			// 8-bit unsigned integer

		private const int CommandResponseHeaderLengthInBytes = 				// Complete Header Length
			CommandResponseLengthFieldLengthInBytes + 
			CommandResponseDataTypeFieldLengthInBytes +
			CommandResponseStatusFieldLengthInBytes;

		// Response
		private static readonly byte[] CommandNotFoundBytesResponse = ASCIIEncoding.ASCII.GetBytes( "Invalid Command" );
		public static readonly byte[] InvalidNumberOfArgumentsResponse = ASCIIEncoding.ASCII.GetBytes( "Invalid number of arguments." );
		public static readonly byte[] InvalidArgumentResponse = ASCIIEncoding.ASCII.GetBytes( "Argument is invalid." );
		public static readonly byte[] InvalidOperationResponse = ASCIIEncoding.ASCII.GetBytes( "The current operation has generated an exception." );

		#endregion

		#region -- Static Attributes --

		[ThreadStatic]
		private static TcpClient _tcpClientThreadStatic = null;

		private static TcpCommandServer _commandServerStatic = null;

		private static bool _passwordReceivedStatic = false;

		#endregion

		#region -- Instance Attributes --

		private int _port = TcpCommandServer.PortDefault;

		private Dictionary<string, ServerCommand> _serverCommands;

		private TcpListener _tcpListener;

		private Collection<TcpClient> _tcpClients = new Collection<TcpClient>();

		private bool _enabled;

		#endregion


		#region -- Constructors --

		/// <summary>
		/// Create the TcpCommandServer singleton instance the first time the Type is accessed.
		/// </summary>
		static TcpCommandServer()
		{
			_commandServerStatic = new TcpCommandServer();
		}

		/// <summary>
		/// Create the a new TcpCommandServer instance. New instances can only be created internally
		/// by static methods.
		/// </summary>
		private TcpCommandServer()
		{
			// Create Process Command Handler Dictionary for retrieval when a command arrives
			_serverCommands = new Dictionary<string, ServerCommand>();

			// Register the built-in commands.
			AddCommand( 
				"#HELP",
				"Returns the list of the available commands.",
				false,
				true,
				new ServerCommandDelegate( GetHelp ) );

			// Password
			AddCommand( 
				"#SET_PASSWORD",
				"Enable/Disable priveledged mode commands. Anything other than a valid password will disable priveledged mode commands.",
				false,
				true,
				new ServerCommandDelegate( SetPassword ) );
		}

		#endregion


		#region -- Public Properties --

		/// <summary>
		/// Get a value that indicates if the Command Manager is currently listening and processing incoming connections.
		/// Use Start/Stop to begin/end command processing.
		/// </summary>
		public bool Enabled
		{
			get { return _enabled; }
		}

		/// <summary>
		/// Get/Set the port on which the TCP command manager receives new client connections. In order for the new
		/// port to be activated, the TCP command manager must be stopped and restarted.
		/// </summary>
		public int Port
		{
			get { return _port; }
			set { _port = value; }
		}

		/// <summary>
		/// Get the number of active clienlient connections.
		/// </summary>
		public int NumberOfClients
		{
			get { return _tcpClients.Count; }
		}

		#endregion


		#region -- Static Methods --

		/// <summary>
		/// Get the CommandManager singleton instance.
		/// </summary>
		/// <returns></returns>
		public static TcpCommandServer GetInstance()
		{
			return _commandServerStatic;
		}

		#endregion

		#region -- Built-in Command Methods --

		/// <summary>
		/// Returns the list of available commands.
		/// </summary>
		/// <param name="commandFields">The recevied command split into an array of strings where each entry represents
		/// a field of the command. The first field is the command name. The remaining field represent the command arguments.</param>
		/// <returns>The response to be transimitted to the requesting client.</returns>
		private CommandExitStatus GetHelp( string[] commandFields, out byte[] responseBytes )
		{
			StringBuilder help = new StringBuilder();

			// Display the list of available commands. Priveledged commands are only listed if the password has
			// been received. Commands created with HideHelpText set true are not displayed.
			foreach ( KeyValuePair<string, ServerCommand> keyValuePair in _serverCommands )
			{
				if ( _passwordReceivedStatic || ( !keyValuePair.Value.PasswordRequired && !keyValuePair.Value.HideHelpText ) )
				{
					help.AppendLine( string.Format( "{0} - {1}", keyValuePair.Value.Name.ToUpper(), keyValuePair.Value.Description ) );
				}
			}

			// Convert to bytes
			responseBytes = ASCIIEncoding.ASCII.GetBytes( help.ToString() );

			return CommandExitStatus.Success;
		}

		/// <summary>
		/// Attempt to enable priveledged mode commands.
		/// </summary>
		/// <param name="commandFields">The recevied command split into an array of strings where each entry represents
		/// a field of the command. The first field is the command name. The remaining field represent the command arguments.</param>
		/// <returns>The response to be transimitted to the requesting client.</returns>
		private CommandExitStatus SetPassword( string[] commandFields, out byte[] responseBytes )
		{
			// Check password
			_passwordReceivedStatic = 
				( commandFields.Length == 2 ) && 
				( commandFields[ 1 ] == "moi12345" );

			// Response
			responseBytes = _passwordReceivedStatic ?
				ASCIIEncoding.ASCII.GetBytes( "Priveledged commands enabled." ) :
				ASCIIEncoding.ASCII.GetBytes( "Priveledged commands disabled." );

			return CommandExitStatus.Success;
		}

		#endregion

		#region -- Private Methods --

		/// <summary>
		/// Receive new incoming connections and create a new thread for communicating with each.
		/// </summary>
		private void ReceiveConnections()
		{
			// Debug
			Debug.WriteLine( "Receive Connections entered." );

			while ( Enabled )
			{
				try
				{
					// Start if necessary
					if ( ( _tcpListener.Server == null ) || !_tcpListener.Server.Connected )
					{
						_tcpListener.Start();
					}

					// Accept new TCP Client
					TcpClient tcpClient = _tcpListener.AcceptTcpClient();

					if ( _tcpClients.Count < MaximumNumberOfConnections )
					{
						// Debug
						Debug.WriteLine( string.Format( "Client connection created for {0}.", tcpClient.Client.RemoteEndPoint ) );

						// Add to the Collection
						_tcpClients.Add( tcpClient );

						// Configure TcpClient
						tcpClient.SendTimeout = DefaultTcpClientSendTimeoutInMilliseconds;
						tcpClient.SendBufferSize = DefaultTcpClientSendBufferSizeInBytes;

						// Start a new thread for processing client commands
						Thread newClientThread = new Thread( new ParameterizedThreadStart( ProcessCommands ) );

						newClientThread.Name = string.Format( "{0} Command Connection", tcpClient.Client.RemoteEndPoint );
						newClientThread.IsBackground = true;
						newClientThread.Start( tcpClient );
					}
					else
					{
						// Close the received connection
						tcpClient.Close();

						// Debug
						Debug.WriteLine( "Client connection denied -- maximum connection count has been reached." );
					}
				}
				catch ( Exception ex )
				{
					// If error occured while enabled, Stop was not called. Add delay to keep from spinning on exception.
					if ( Enabled )
					{
						// Debug
						Debug.WriteLine( string.Format( "Failure in Receive Connection: {0}\r\n\r\n{1}", 
						                               MoiException.GetAllMessages( ex ), 
						                               ex.StackTrace ) );

						// Stop current Listener (this will create a new underlying Socket)
						_tcpListener.Stop();

						// Delay
						Thread.Sleep( 200 );
					}
				}
			}

			// Debug
			Debug.WriteLine( "Receive Connections exited." );
		}

		/// <summary>
		/// Process incoming commands.
		/// </summary>
		/// <param name="parameter"></param>
		private void ProcessCommands( object parameter )
		{
			string[] commandFields = null;

			NetworkStream networkStream = null;
			ServerCommand command;

			int bytesReceived = 0;
			int byteIndex = 0;

			byte[] commandBytes = new byte[ CommandMaximumSizeInBytes ];
			byte[] responseDataBytes = null;
			byte[] responseHeaderBytes = new byte[ CommandResponseHeaderLengthInBytes ];

			CommandExitStatus commandStatus;

			// Debug
			Debug.WriteLine( "Process Commands: Entered" );

			// Cast parameter as TCP Client
			_tcpClientThreadStatic = (TcpClient) parameter;

			try
			{
				// Get Client/Module Newtork Stream
				networkStream = _tcpClientThreadStatic.GetStream();

				while ( true )
				{
					// Read Next Byte
					bytesReceived = networkStream.Read( commandBytes, byteIndex, 1 );

					if ( bytesReceived != 0 )
					{
						// Process?
						if ( ( commandBytes[ byteIndex ] == CommandTermination ) || ( ++byteIndex == CommandMaximumSizeInBytes ) )
						{
							// Split the received command into fields
							commandFields = ASCIIEncoding.ASCII.GetString( commandBytes, 0, byteIndex ).Split(
								CommandFieldDelimiters, StringSplitOptions.RemoveEmptyEntries );

							if ( commandFields.Length > 0 )
							{
								// Reset Response to Command Not Found
								responseDataBytes = CommandNotFoundBytesResponse;

								// Process the Command
								try
								{
									if ( !_serverCommands.TryGetValue( commandFields[ 0 ].ToUpper(), out command ) )
									{
										commandStatus = CommandExitStatus.InvalidCommand;
									}
									else if ( !( !command.PasswordRequired || _passwordReceivedStatic || command.Name.ToUpper() == "#SET_PASSWORD" ) )
									{
										commandStatus = CommandExitStatus.AccessDenied;
									}
									else
									{
										commandStatus = command.CommandDelegate( commandFields, out responseDataBytes );
									}
								}
								catch( Exception ex )
								{
									// Debug
									Debug.WriteLine( string.Format( 
									              "'{0}' encountered the following error and the connection has been closed: {1}",
									              commandFields[ 0 ], 
									              MoiException.GetAllMessages( ex ) ) );

									// Exit command processing for this remote client
									break;
								}

								lock ( networkStream )
								{
									// Length
									Array.Copy( BitConverter.GetBytes( (uint) responseDataBytes.Length ), responseHeaderBytes, 
										CommandResponseLengthFieldLengthInBytes );

									// Type
									responseHeaderBytes[ CommandResponseLengthFieldLengthInBytes ] = (byte) PacketType.CommandResponse;

									// Status
									responseHeaderBytes[ CommandResponseLengthFieldLengthInBytes + CommandResponseDataTypeFieldLengthInBytes ] = 
										(byte) commandStatus;

									// Write Header to network stream
									networkStream.Write( responseHeaderBytes, 0, CommandResponseHeaderLengthInBytes );

									// Write Response to network stream.
									networkStream.Write( responseDataBytes, 0, responseDataBytes.Length );
								}
							}

							// Reset index
							byteIndex = 0;
						}
					}
					else
					{
						// Debug
						Debug.WriteLine( "Process Commands: Zero Bytes Read" );

						// Exit Command Processing for remot client
						break;
					}
				}
			}
			catch ( Exception ex )
			{
				// Display error message when debugging
				string message;

				if ( ( commandFields != null ) && ( commandFields.Length > 0 ) )
				{
					message = string.Format( "'{0}' communication failure: {1}", commandFields[ 0 ], MoiException.GetAllMessages( ex ) );
				}
				else
				{
					message = "Remote communication failed: " + MoiException.GetAllMessages( ex );
				}
				
				Debug.WriteLine( message );
			}

			// Disconnect
			try
			{
				if ( _tcpClientThreadStatic.Connected )
				{
					if ( networkStream != null )
					{
						// Close the Network Stream
						networkStream.Close();
					}

					// Close the TCP Client
					_tcpClientThreadStatic.Close();
				}
			}
			catch
			{
				// Do nothing
			}

			// Remove the TCP Client in a threadsafe manner.
			lock ( _tcpClients )
			{
				if ( ( _tcpClientThreadStatic != null ) && ( _tcpClients.Contains( _tcpClientThreadStatic ) ) )
				{
					_tcpClients.Remove( _tcpClientThreadStatic );
				}
			}

			// Debug
			Debug.WriteLine( "Process Commands: Exited" );
		}

		#endregion

		#region -- Public Methods --

		/// <summary>
		/// Start receiving incoming connections and processing commands.
		/// </summary>
		/// <returns>true if the command manager active following the completion of the Start method; false otherwise.</returns>
		public bool Start()
		{
			if ( !Enabled )
			{
				try
				{
					// Create Incoming Connection Listener
					_tcpListener = new TcpListener( new IPEndPoint( IPAddress.Any, Port ) );

					// Attempt to Listen for connections
					_tcpListener.Start();

					// Interface is Enabled
					_enabled = true;

					// Receive Connection on a separate thread
					Thread receiveConnectionThread = new Thread( new ThreadStart( ReceiveConnections ) );

					receiveConnectionThread.Name = "Receive Command Connections";
					receiveConnectionThread.IsBackground = true;

					receiveConnectionThread.Start();

				}
				catch ( Exception ex )
				{
					// Debug
					Debug.WriteLine( "Receive Connections failed to start: " + MoiException.GetAllMessages( ex ) );
				}
			}

			return Enabled;
		}

		/// <summary>
		/// Close all active connections and stop listening for new connections.
		/// </summary>
		public void Stop()
		{
			if ( _enabled )
			{
				// Disable
				_enabled = false;

				// Stop receiving incoming connections
				_tcpListener.Stop();

				// Close existing Connections
				lock ( _tcpClients )
				{
					for ( int index = _tcpClients.Count - 1; index >= 0; index-- )
					{
						// Client
						if ( _tcpClients[ index ].Connected )
						{
							try
							{
								_tcpClients[ index ].GetStream().Close();
							}
							catch
							{
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Add a new server command.
		/// </summary>
		public void AddCommand( string name, string description, bool passwordRequired, bool hideHelpText,
		                              ServerCommandDelegate command )
		{
			// Add a new ServerCommand
			_serverCommands[ name.ToUpper() ] = 
				new ServerCommand( name, description, passwordRequired, hideHelpText, command );
		}

		/// <summary>
		/// Remove a command.
		/// </summary>
		/// <param name="commandText">The name of the command (ie #IDN).</param>
		public void RemoveCommandHandler( string commandText )
		{
			_serverCommands.Remove( commandText.ToUpper() );
		}

//		/// <summary>
//		/// Return the TCP Client for the calling thread. This is useful for when registered command need to reference the TCP
//		/// Socket used to communicate with the remote client directly instead of just returning a byte array result.
//		/// </summary>
//		/// <returns></returns>
//		public TcpClient GetTcpClient()
//		{
//			return _tcpClientThreadStatic;
//		}
//
//		/// <summary>
//		/// Transmit the payload to the remote client for the current thread. If an excpetion is generated, 
//		/// the TCP network stream and connection are closed.
//		/// </summary>
//		/// <param name="type">The type of the payload.</param>
//		/// <param name="payload">The data to be sent to the remote client.</param>
//		/// <returns>true if the packet was sent without exception; false otherwise.</returns>
//		public bool SendPacket( PacketType type, byte[] payload )
//		{
//			return SendPacket( type, payload, _tcpClientThreadStatic );
//		}
//
//		/// <summary>
//		/// Transmit the payload to the remote client. If an excpetion is generated, the network stream and connection are closed.
//		/// </summary>
//		/// <param name="type">The type of the payload.</param>
//		/// <param name="payload">The data to be sent to the remote client.</param>
//		/// <param name="tcpClient">The TCP connection to the remote client.</param>
//		/// <returns>true if the packet was sent without exception; false otherwise.</returns>
//		public bool SendPacket( PacketType type, byte[] payload, TcpClient tcpClient )
//		{
//			bool success = false;
//
//			// Create complete packet to transmit in one operation
//			byte[] bytes = new byte[ CommandResponseHeaderLengthInBytes + payload.Length ];
//
//			// Length
//			Array.Copy( BitConverter.GetBytes( (uint) payload.Length ), bytes, CommandResponseLengthFieldLengthInBytes );
//
//			// Packet Type
//			bytes[ CommandResponseLengthFieldLengthInBytes ] = (byte) type;
//
//			// Command Status
//			bytes[ CommandResponseLengthFieldLengthInBytes + CommandResponseDataTypeFieldLengthInBytes ] = 
//				(byte) CommandExitStatus.Success;
//
//			// Payload
//			Array.Copy( payload, 0, bytes, CommandResponseHeaderLengthInBytes, payload.Length );						
//
//			NetworkStream networkStream = null;
//
//			try
//			{
//				if ( tcpClient.Connected )
//				{
//					// Get Network Stream
//					networkStream = tcpClient.GetStream();
//
//					// Transmit
//					lock ( networkStream )
//					{
//						// Write to network
//						networkStream.Write( bytes, 0, bytes.Length );
//					}
//
//					// Failed
//					success = true;
//				}
//			}
//			catch
//			{
//				// Close network stream
//				if ( networkStream != null )
//				{
//					networkStream.Close();
//				}
//
//				// Close TCP Client
//				if ( ( tcpClient != null ) && tcpClient.Connected )
//				{
//					tcpClient.Close();
//				}
//			}
//
//			return success;
//		}

		#endregion
	}
}
