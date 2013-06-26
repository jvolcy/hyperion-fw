using System;
using System.Text;


using MicronOptics.Hyperion.Interrogator.Device;
using MicronOptics.Hyperion.Interrogator.Server;


namespace MicronOptics.Hyperion.Interrogator.Controller
{
	public class HyperionController
	{
		#region -- Instance Variables --

		HyperionDevice _hyperion;

		#endregion


		#region -- Constructors --

		private HyperionController( HyperionDevice hyperion )
		{
			_hyperion = hyperion;
		}

		#endregion


		#region -- Static Methods --

		public static HyperionController Create( HyperionDevice device, params  ICommandServer[] servers )
		{
			HyperionController controller = new HyperionController( device );

			// Connect each of the command servers to the controller
			foreach ( ICommandServer server in servers )
			{
				controller.ConnectCommandServer( server );
			}

			return controller;
		}

		#endregion
		
		#region -- Private Methods --

		/// <summary>
		/// Gets the decimal or hexidecimal value of a string.
		/// </summary>
		/// <returns>The decimal or hexidecimal value that the string represents.</returns>
		/// <param name="valueAsString">Hexadecimal string to be converted.</param>
		private uint GetDecimalOrHexidecimalValueFromString( string valueAsString )
		{
			return 	Convert.ToUInt32( valueAsString, 
			                         valueAsString.ToLower().Contains("0x") ? 16 : 10 );
		}

		/// <summary>
		/// Reads the specified register address from the device..
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">The value stored in the register (as a hexadecimal string).</param>
		private CommandExitStatus ReadRegister( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Read from device
				uint val = _hyperion.ReadRegister(
					GetDecimalOrHexidecimalValueFromString( commandFields[ 1 ] ) );

				// Display
				Console.WriteLine( string.Format( "{0}: Value {1} read from address {2}",
				                                 DateTime.Now.ToLongTimeString(), val.ToString( "X8" ), commandFields[1] ) );

				// Response
				responseBytes = ASCIIEncoding.ASCII.GetBytes( "0x" + val.ToString( "X8" ) );

				return CommandExitStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandExitStatus.ErrorProcessing;
			}
		}

		/// <summary>
		/// Write a value to a device register.
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">Response bytes.</param>
		private CommandExitStatus WriteRegister( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Write to device
				_hyperion.WriteRegister(
					GetDecimalOrHexidecimalValueFromString( commandFields[ 1 ] ),
					GetDecimalOrHexidecimalValueFromString( commandFields[ 2 ] ) );

				// Display
				Console.WriteLine( string.Format( "{0}: Value {1} written to address {2}",
				                                 DateTime.Now.ToLongTimeString(), commandFields[ 2 ], commandFields[ 1 ] ) );

				// Response
				responseBytes = ASCIIEncoding.ASCII.GetBytes( "SUCCESS" );

				return CommandExitStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandExitStatus.ErrorProcessing;
			}
		}

		/// <summary>
		/// Gets the raw peak data as transferred by the hardware (FPGA).
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">Response bytes.</param>
		private CommandExitStatus GetRawPeaks( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Point the response at the copied data
				responseBytes = _hyperion.GetRawPeakData();

				// Display
				Console.WriteLine( string.Format( "{0}: --> Get Raw Peaks returned {1} bytes",
				                                 DateTime.Now.ToLongTimeString(), responseBytes.Length ) );

				// Wahoo...it freaking worked...
				return CommandExitStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandExitStatus.ErrorProcessing;
			}
		}

		/// <summary>
		/// Gets the raw peak data as transferred by the hardware (FPGA).
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">Response bytes.</param>
		private CommandExitStatus GetRawSpectra( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Point the response at the copied data
				responseBytes = _hyperion.GetRawSpectrumData(); 

				// Display
				Console.WriteLine( string.Format( "{0}: ----> Get Raw Spectra returned {1} bytes",
				                                 DateTime.Now.ToLongTimeString(), responseBytes.Length ) );
				// Wahoo...it freaking worked...
				return CommandExitStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandExitStatus.ErrorProcessing;
			}
		}

		#endregion

		#region -- Public Methods --

		/// <summary>
		/// Connect the command server to the Hyperion Device specific functions.
		/// </summary>
		/// <param name="commandServer">Command server processing Hyperion Device remote commands.</param>
		private void ConnectCommandServer( ICommandServer commandServer )
		{
			commandServer.AddCommand( 
			                          "#ReadRegister",
			                          "Read the specified device register address.",
			                          false,
			                          false,
			                          new ServerCommandDelegate( ReadRegister ) );

			commandServer.AddCommand( 
			                          "#WriteRegister",
			                          "Write value to the specified device register address.",
			                          false,
			                          false,
			                          new ServerCommandDelegate( WriteRegister ) );

			commandServer.AddCommand( 
			                          "#GetRawPeaks",
			                          "Get the raw (as transferred from the FPGA) peak data.",
			                          false,
			                          false,
			                          new ServerCommandDelegate( GetRawPeaks ) );

			commandServer.AddCommand( 
			                          "#GetRawSpectra",
			                          "Get the raw (as transferred from the FPGA) reflected optical spectrum data.",
			                          false,
			                          false,
			                          new ServerCommandDelegate( GetRawSpectra ) );
		}

		#endregion
	}
}

