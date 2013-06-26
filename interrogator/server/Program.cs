using System;
using System.Text;
using System.Runtime.InteropServices;

using MicronOptics.Hyperion.Interrogator.Device;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	class MainClass
	{
		private static HyperionDevice _Device = null;

		public static void Main( string[] args )
		{
			// A HardwareDevice provides communication to the device FPGA
			// through a kernel device driver.
			using( _Device = new HyperionDevice( HardwareInterface.Create() ) )
			{
				#region -- Create and Initialize Command Manager --

				// Create a new command manager to receive and process incoming requests.
				TcpCommandServer commandManager = TcpCommandServer.GetInstance();

				commandManager.AddCommand( 
					"#ReadRegister",
					"Read the specified device register address.",
					false,
					false,
					new ServerCommandDelegate( ReadRegister ) );

				commandManager.AddCommand( 
					"#WriteRegister",
					"Write value to the specified device register address.",
					false,
					false,
					new ServerCommandDelegate( WriteRegister ) );

				commandManager.AddCommand( 
					"#GetRawPeaks",
					"Get the raw (as transferred from the FPGA) peak data.",
					false,
					false,
					new ServerCommandDelegate( GetRawPeaks ) );

				commandManager.AddCommand( 
					"#GetRawSpectra",
					"Get the raw (as transferred from the FPGA) reflected optical spectrum data.",
					false,
					false,
					new ServerCommandDelegate( GetRawSpectra ) );

				// Recieve and process incoming commands
				commandManager.Start();

				#endregion

				#region -- Display FPGA and Driver Info --

				// Provide an easy sign that the communication with the device
				// is functioning
				Console.WriteLine( string.Format( "  FPGA Version: {0}",
				ASCIIEncoding.ASCII.GetString( BitConverter.GetBytes( _Device.GetFpgaVersion() ) ) ) );

				// Provide an easy sign that the communication with the device
				// is functioning
				Console.WriteLine( string.Format( "Driver Version: {0}",
				_Device.GetDeviceDriverVersion() ) );

				Console.WriteLine( "----------------------" );

				#endregion

				// Wait for user input to exit gracefully 
				Console.ReadLine();

				// Before exiting, propertly shutdown the device to allow for easy
				// start and stop debugging (no reboot ).
				commandManager.Stop();
			}
		}

		/// <summary>
		/// Gets the decimal or hexidecimal value of a string.
		/// </summary>
		/// <returns>The decimal or hexidecimal value that the string represents.</returns>
		/// <param name="valueAsString">Hexadecimal string to be converted.</param>
		private static uint GetDecimalOrHexidecimalValueFromString( string valueAsString )
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
		private static CommandExitStatus ReadRegister( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Read from device
				uint val = _Device.ReadRegister(
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
		private static CommandExitStatus WriteRegister( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Write to device
				_Device.WriteRegister(
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
		private static unsafe CommandExitStatus GetRawPeaks( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Point the response at the copied data
				responseBytes = _Device.GetRawPeakData();; 

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
		private static unsafe CommandExitStatus GetRawSpectra( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Point the response at the copied data
				responseBytes = _Device.GetRawSpectrumData(); 

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
	}
}
