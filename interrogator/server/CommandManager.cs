using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	/// <summary>
	/// The Command Manager is responsible for external communication with ENLIGHT Pro.
	/// </summary>
	public class CommandManager 
	{
		#region -- Standard Command Responses --

		public static readonly string True = "1";
		public static readonly string False = "0";

		private static readonly byte[] mCommandNotFoundBytes = ASCIIEncoding.ASCII.GetBytes( "Invalid Command" );
		public static readonly byte[] InvalidNumberOfArguments = ASCIIEncoding.ASCII.GetBytes( "Invalid number of arguments." );
		public static readonly byte[] InvalidArgument = ASCIIEncoding.ASCII.GetBytes( "Argument is invalid." );
		public static readonly byte[] InvalidOperation = ASCIIEncoding.ASCII.GetBytes( "The current operation has generated an exception." );

		#endregion

		#region -- Constants --

		private const int mMaximumNumberOfConnections = 5;

		private const int mCommandMaximumSizeInBytes = 2048;
		private const char mCommandTermination = '\n';

		private readonly char[] mCommandFieldDelimiters = new char[] { ' ', ',', '\n' };

		private const int mCommandResponseLengthFieldLengthInBytes = 4;			// 32-bit unsigned integer
		private const int mCommandResponseDataTypeFieldLengthInBytes = 1;		// 8-bit unsigned integer
		private const int mCommandResponseStatusFieldLengthInBytes = 1;			// 8-bit unsigned integer
		private const int mCommandResponseHeaderLengthInBytes = 
			mCommandResponseLengthFieldLengthInBytes + 
			mCommandResponseDataTypeFieldLengthInBytes +
			mCommandResponseStatusFieldLengthInBytes;

		private const int mDefaultTcpClientSendTimeoutInMilliseconds = 1000;
		private const int mDefaultTcpClientSendBufferSizeInBytes = 128 * 1024;	// 128 kbytes


		#endregion

		#region -- Thread Static Attributes --

		[ThreadStatic]
		private static TcpClient mTcpClientThreadStatic = null;

		#endregion

		#region -- Static Attributes --

		public const int PortDefault = 1853;

		public const int PortMiniumum = 1000;
		public const int PortMaximum = 65535;

		private static CommandManager sCommandManager = null;

		private static bool mPasswordReceived = false;

		#endregion

		#region -- Instance Attributes --

		private int mPort = CommandManager.PortDefault;

		private Dictionary<string, EnlightCommand> mProcessCommandHandlers;

		private TcpListener mTcpListener;

		private Collection<TcpClient> mTcpClients = new Collection<TcpClient>();

		private bool mEnabled;

		#endregion


		#region -- Constructors --

		/// <summary>
		/// Create the MoiCommandManager singleton instance.
		/// </summary>
		static CommandManager()
		{
			sCommandManager = new CommandManager();
		}

		/// <summary>
		/// Create the singleton Micron Optics Command Manager instance.
		/// </summary>
		private CommandManager()
		{
			// Create Process Command Handler Dictionary for retrieval when a command arrives
			this.mProcessCommandHandlers = new Dictionary<string, EnlightCommand>();

			#region -- Remote Commands --

			// Help
			this.AddCommandHandler( new EnlightCommand(
				"#HELP",
				"Returns the list of the available remote interface ENLIGHT commands.",
				false,
				true,
				new EnlightCommandDelegate( GetHelp ) ) );

			// Password
			this.AddCommandHandler( new EnlightCommand(
				"#SET_PASSWORD",
				"Enable/Disable priveledged mode ENLIGHT commands. Anything other than a valid password will disable priveledged mode commands.",
				false,
				true,
				new EnlightCommandDelegate( SetPassword ) ) );

			#endregion
		}

		#endregion


		#region -- Protected Properties --

		#endregion

		#region -- Public Properties --

		/// <summary>
		/// Get a value that indicates if the Command Manager is current listening and processing incoming connections.
		/// </summary>
		public bool Enabled
		{
			get { return this.mEnabled; }
		}

		/// <summary>
		/// Get/Set the port on which the command manager receives new client connections. In order for the new
		/// port to be activated, the command manager must be stopped and restarted.
		/// </summary>
		public int Port
		{
			get { return this.mPort; }
			set { this.mPort = value; }
		}

		/// <summary>
		/// Get the number of Client connections.
		/// </summary>
		public int NumberOfClients
		{
			get { return this.mTcpClients.Count; }
		}

		#endregion


		#region -- Static Methods --

		/// <summary>
		/// Get the CommandManager singleton instance.
		/// </summary>
		/// <returns></returns>
		public static CommandManager GetInstance()
		{
			return sCommandManager;
		}

		/// <summary>
		/// Determine if a received command is a GET command.
		/// </summary>
		/// <param name="commandText">The command text.</param>
		/// <returns>true if the command text contains the text "#GET"; false otherwise.</returns>
		public static bool IsGetCommand( string commandText )
		{
			return commandText.ToUpper().StartsWith( "#GET_" );
		}

		/// <summary>
		/// Determine if a received command is a SET command.
		/// </summary>
		/// <param name="commandText">The command text.</param>
		/// <returns>true if the command text contains the text "#SET"; false otherwise.</returns>
		public static bool IsSetCommand( string commandText )
		{
			return commandText.ToUpper().StartsWith( "#SET_" );
		}

		#endregion

		#region -- ENLIGHT Command Methods --

		/// <summary>
		/// Returns the list of ENLIGHT remote interface commands that are available.
		/// </summary>
		/// <param name="commandFields">The recevied command split into an array of strings where each entry represents
		/// a field of the command. The first field is the command name. The remaining field represent the command arguments.</param>
		/// <returns>The response to be transimitted to the requesting client.</returns>
		private CommandStatus GetHelp( string[] commandFields, out byte[] responseBytes )
		{
			StringBuilder help = new StringBuilder();

			foreach ( KeyValuePair<string, EnlightCommand> keyValuePair in this.mProcessCommandHandlers )
			{
				if ( mPasswordReceived || ( !keyValuePair.Value.PasswordRequired && !keyValuePair.Value.HideHelpText ) )
				{
					help.AppendLine( string.Format( "{0} - {1}", keyValuePair.Value.CommandText.ToUpper(), keyValuePair.Value.Description ) );
				}
			}

			// Convert to bytes
			responseBytes = ASCIIEncoding.ASCII.GetBytes( help.ToString() );

			return CommandStatus.Success;
		}

		/// <summary>
		/// Attempt to enable priveledged mode commands.
		/// </summary>
		/// <param name="commandFields">The recevied command split into an array of strings where each entry represents
		/// a field of the command. The first field is the command name. The remaining field represent the command arguments.</param>
		/// <returns>The response to be transimitted to the requesting client.</returns>
		private CommandStatus SetPassword( string[] commandFields, out byte[] responseBytes )
		{
			// Check password
			mPasswordReceived = 
				( commandFields.Length == 2 ) && 
				( commandFields[ 1 ] == "moi12345" );

			// Response
			responseBytes = mPasswordReceived ?
				ASCIIEncoding.ASCII.GetBytes( "Priveledged commands enabled." ) :
				ASCIIEncoding.ASCII.GetBytes( "Priveledged commands disabled." );

			return CommandStatus.Success;
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

			while ( this.Enabled )
			{
				try
				{
					// Start if necessary
					if ( ( this.mTcpListener.Server == null ) || !this.mTcpListener.Server.Connected )
					{
						this.mTcpListener.Start();
					}

					// Accept new TCP Client
					TcpClient tcpClient = this.mTcpListener.AcceptTcpClient();

					if ( this.mTcpClients.Count < mMaximumNumberOfConnections )
					{
						// Debug
						Debug.WriteLine( string.Format( "Client connection created for {0}.", tcpClient.Client.RemoteEndPoint ) );

						// Add to the Collection
						this.mTcpClients.Add( tcpClient );

						// Configure TcpClient
						tcpClient.SendTimeout = mDefaultTcpClientSendTimeoutInMilliseconds;
						tcpClient.SendBufferSize = mDefaultTcpClientSendBufferSizeInBytes;

						//tcpClient.ReceiveTimeout = mDefaultTcpClientSendTimeoutInMilliseconds;

						// Start a new thread for processing client commands
						Thread clientThread = new Thread( new ParameterizedThreadStart( ProcessCommands ) );

						clientThread.Name = string.Format( "{0} Command Connection", tcpClient.Client.RemoteEndPoint );
						clientThread.IsBackground = true;
						clientThread.Start( tcpClient );
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
					if ( this.Enabled )
					{
						// Debug
						Debug.WriteLine( string.Format( "Failure in Receive Connection: {0}\r\n\r\n{1}", MoiException.GetAllMessages( ex ), 
							ex.StackTrace ) );

						// Stop current Listener (this will create a new underlying Socket)
						this.mTcpListener.Stop();

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
			EnlightCommand enlightCommand;

			int bytesReceived = 0;
			int byteIndex = 0;

			byte[] commandBytes = new byte[ mCommandMaximumSizeInBytes ];
			byte[] responseDataBytes = null;
			byte[] responseHeaderBytes = new byte[ mCommandResponseHeaderLengthInBytes ];

			CommandStatus commandStatus;

			// Debug
			Debug.WriteLine( "Process Commands: Entered" );

			// Cast parameter as TCP Client
			mTcpClientThreadStatic = (TcpClient) parameter;

			try
			{
				// Get Client/Module Newtork Stream
				networkStream = mTcpClientThreadStatic.GetStream();

				while ( true )
				{
					// Read Next Byte
					bytesReceived = networkStream.Read( commandBytes, byteIndex, 1 );

					if ( bytesReceived != 0 )
					{
						// Process?
						if ( ( commandBytes[ byteIndex ] == mCommandTermination ) || ( ++byteIndex == mCommandMaximumSizeInBytes ) )
						{
							// Split the received command into fields
							commandFields = ASCIIEncoding.ASCII.GetString( commandBytes, 0, byteIndex ).Split(
								mCommandFieldDelimiters, StringSplitOptions.RemoveEmptyEntries );

							if ( commandFields.Length > 0 )
							{
								// Reset Response to Command Not Found
								responseDataBytes = mCommandNotFoundBytes;

								// Process the Command
								try
								{
									if ( !this.mProcessCommandHandlers.TryGetValue( commandFields[ 0 ].ToUpper(), out enlightCommand ) )
									{
										commandStatus = CommandStatus.InvalidCommand;
									}
									else if ( !( !enlightCommand.PasswordRequired || mPasswordReceived || enlightCommand.CommandText.ToUpper() == "#SET_PASSWORD" ) )
									{
										commandStatus = CommandStatus.AccessDenied;
									}
									else
									{
										commandStatus = enlightCommand.CommandDelegate( commandFields, out responseDataBytes );
									}

									//success = (
									//    this.mProcessCommandHandlers.TryGetValue( commandFields[ 0 ].ToUpper(), out enlightCommand ) &&
									//    ( !enlightCommand.PasswordRequired || mPasswordReceived || enlightCommand.CommandText.ToUpper() == "#SET_PASSWORD" ) &&
									//    ( ( enlightCommand.CommandDelegate( commandFields, out responseDataBytes ) & CommandStatus.Success ) > 0 ) );
								}
								catch( Exception ex )
								{
									// Debug
									Debug.WriteLine( string.Format( "'{0}' encountered the following error and the connection has been closed: {1}",
										commandFields[ 0 ], MoiException.GetAllMessages( ex ) ) );

									// Exit command processing for this remote client
									break;
								}

								lock ( networkStream )
								{
									// Length
									Array.Copy( BitConverter.GetBytes( (uint) responseDataBytes.Length ), responseHeaderBytes, 
										mCommandResponseLengthFieldLengthInBytes );

									// Type
									responseHeaderBytes[ mCommandResponseLengthFieldLengthInBytes ] = (byte) PacketType.CommandResponse;

									// Status
									responseHeaderBytes[ mCommandResponseLengthFieldLengthInBytes + mCommandResponseDataTypeFieldLengthInBytes ] = 
										(byte) commandStatus;

									// Write Header to network stream
									networkStream.Write( responseHeaderBytes, 0, mCommandResponseHeaderLengthInBytes );

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
				#region -- Log EventData --

				// Create EventData Message
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

				#endregion
			}

			// Disconnect
			try
			{
				if ( mTcpClientThreadStatic.Connected )
				{
					if ( networkStream != null )
					{
						// Close the Network Stream
						networkStream.Close();
					}

					// Close the TCP Client
					mTcpClientThreadStatic.Close();
				}
			}
			catch
			{
				// Do nothing
			}

			// Remove
			lock ( this.mTcpClients )
			{
				if ( ( mTcpClientThreadStatic != null ) && ( this.mTcpClients.Contains( mTcpClientThreadStatic ) ) )
				{
					this.mTcpClients.Remove( mTcpClientThreadStatic );
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
			if ( !this.Enabled )
			{
				try
				{
					// Create Incoming Connection Listener
					this.mTcpListener = new TcpListener( new IPEndPoint( IPAddress.Any, this.Port ) );

					// Attempt to Listen for connections
					this.mTcpListener.Start();

					// Interface is Enabled
					this.mEnabled = true;

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

			return this.Enabled;
		}

		/// <summary>
		/// Close all active connections and stop listening for new connections.
		/// </summary>
		public void Stop()
		{
			if ( this.mEnabled )
			{
				// Disable
				this.mEnabled = false;

				// Stop receiving incoming connections
				this.mTcpListener.Stop();

				// Close existing Connections
				lock ( this.mTcpClients )
				{
					for ( int index = this.mTcpClients.Count - 1; index >= 0; index-- )
					{
						// Client
						if ( this.mTcpClients[ index ].Connected )
						{
							try
							{
								this.mTcpClients[ index ].GetStream().Close();
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
		/// Add a command.
		/// </summary>
		public void AddCommandHandler( EnlightCommand enlightCommand )
		{
			this.mProcessCommandHandlers[ enlightCommand.CommandText.ToUpper() ] = enlightCommand;
		}

		/// <summary>
		/// Remove a command.
		/// </summary>
		/// <param name="commandText">The name of the command (ie #IDN).</param>
		public void RemoveCommandHandler( string commandText )
		{
			this.mProcessCommandHandlers.Remove( commandText );
		}

		/// <summary>
		/// Return the TCP Client for the calling thread. This is useful for when registered command need to reference the TCP
		/// Socket used to communicate with the remote client directly instead of just returning a byte array result.
		/// </summary>
		/// <returns></returns>
		public TcpClient GetTcpClient()
		{
			return mTcpClientThreadStatic;
		}

		/// <summary>
		/// Transmit the payload to the remote client for the current thread. If an excpetion is generated, 
		/// the TCP network stream and connection are closed.
		/// </summary>
		/// <param name="type">The type of the payload.</param>
		/// <param name="payload">The data to be sent to the remote client.</param>
		/// <returns>true if the packet was sent without exception; false otherwise.</returns>
		public bool SendPacket( PacketType type, byte[] payload )
		{
			return SendPacket( type, payload, mTcpClientThreadStatic );
		}

		/// <summary>
		/// Transmit the payload to the remote client. If an excpetion is generated, the network stream and connection are closed.
		/// </summary>
		/// <param name="type">The type of the payload.</param>
		/// <param name="payload">The data to be sent to the remote client.</param>
		/// <param name="tcpClient">The TCP connection to the remote client.</param>
		/// <returns>true if the packet was sent without exception; false otherwise.</returns>
		public bool SendPacket( PacketType type, byte[] payload, TcpClient tcpClient )
		{
			bool success = false;

			// Create complete packet to transmit in one operation
			byte[] bytes = new byte[ mCommandResponseHeaderLengthInBytes + payload.Length ];

			// Length
			Array.Copy( BitConverter.GetBytes( (uint) payload.Length ), bytes, mCommandResponseLengthFieldLengthInBytes );

			// Packet Type
			bytes[ mCommandResponseLengthFieldLengthInBytes ] = (byte) type;

			// Command Status
			bytes[ mCommandResponseLengthFieldLengthInBytes + mCommandResponseDataTypeFieldLengthInBytes ] = 
				(byte) CommandStatus.Success;

			// Payload
			Array.Copy( payload, 0, bytes, mCommandResponseHeaderLengthInBytes, payload.Length );						

			NetworkStream networkStream = null;

			try
			{
				if ( tcpClient.Connected )
				{
					// Get Network Stream
					networkStream = tcpClient.GetStream();

					// Transmit
					lock ( networkStream )
					{
						// Write to network
						networkStream.Write( bytes, 0, bytes.Length );
					}

					// Failed
					success = true;
				}
			}
			catch
			{
				// Close network stream
				if ( networkStream != null )
				{
					networkStream.Close();
				}

				// Close TCP Client
				if ( ( tcpClient != null ) && tcpClient.Connected )
				{
					tcpClient.Close();
				}
			}

			return success;
		}

		#endregion
	}
}
