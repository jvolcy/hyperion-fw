using System;
using System.Text;
using System.Runtime.InteropServices;
using MicronOptics.Hyperion.Interrogator.Device;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	class MainClass
	{
		private static HardwareDevice _Device = null;

		private static byte[] _RawPeakData;
		private static byte[] _RawSpectrumData;

		public static void Main( string[] args )
		{
			#region -- Create and Initialize Device --

			// A HardwareDevice provides communication to the device FPGA
			// through a kernel device driver.
			_Device = HardwareDevice.Create();

			// The device hardware (FPGA) specifies to the device driver the size and number of peak/spectrum
			// DMA buffers. The size of the DMA buffers is needed to allocate the managed memory for returning
			// in response to GetRawPeakData/GetRawSpectrumData commads.
			_RawPeakData = new byte[ _Device.PeakDmaBufferSizeInBytes ];
			_RawSpectrumData = new byte[ _Device.SpectrumDmaBufferSizeInBytes ];

			#endregion

			#region -- Create and Initialize Command Manager --

			// Create a new command manager to receive and process incoming requests.
			CommandManager commandManager = CommandManager.GetInstance();

			commandManager.AddCommandHandler( 
			                   new EnlightCommand( 
			                   "#ReadRegister",
			                   "Read the specified device register address.",
			                   false,
			                   false,
			                   new EnlightCommandDelegate( ReadRegister ) ) );

			commandManager.AddCommandHandler( 
			                   new EnlightCommand( 
			                   "#WriteRegister",
			                   "Write value to the specified device register address.",
			                   false,
			                   false,
			                   new EnlightCommandDelegate( WriteRegister ) ) );

			commandManager.AddCommandHandler( 
			                   new EnlightCommand( 
			                   "#GetRawPeaks",
			                   "Get the raw (as transferred from the FPGA) peak data.",
			                   false,
			                   false,
			                   new EnlightCommandDelegate( GetRawPeaks ) ) );

			commandManager.AddCommandHandler( 
			                   new EnlightCommand( 
			                   "#GetRawSpectra",
			                   "Get the raw (as transferred from the FPGA) reflected optical spectrum data.",
			                   false,
			                   false,
			                   new EnlightCommandDelegate( GetRawSpectra ) ) );

			// Recieve and process incoming commands
			commandManager.Start();

			#endregion

			#region -- Display FPGA and Driver Info --

			// Provide an easy sign that the communication with the device
			// is functioning
			Console.WriteLine( string.Format( "  FPGA Version: {0}",
				ASCIIEncoding.ASCII.GetString( BitConverter.GetBytes( _Device.ReadRegister( DeviceRegisterAddress.FpgaVersion ) ) ) ) );

			// Provide an easy sign that the communication with the device
			// is functioning
			Console.WriteLine( string.Format( "Driver Version: {0}",
				_Device.GetDriverVersion() ) );

			Console.WriteLine( "----------------------" );

			#endregion

			// Wait for user input to exit gracefully 
			Console.ReadLine();

			// Before exiting, propertly shutdown the device to allow for easy
			// start and stop debugging (no reboot ).
			commandManager.Stop();
			_Device.Close();
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
		private static CommandStatus ReadRegister( string[] commandFields, out byte[] responseBytes )
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

				return CommandStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandStatus.ErrorProcessing;
			}
		}

		/// <summary>
		/// Write a value to a device register.
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">Response bytes.</param>
		private static CommandStatus WriteRegister( string[] commandFields, out byte[] responseBytes )
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

				return CommandStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandStatus.ErrorProcessing;
			}
		}

		/// <summary>
		/// Gets the raw peak data as transferred by the hardware (FPGA).
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">Response bytes.</param>
		private static unsafe CommandStatus GetRawPeaks( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Copy the raw peak data from the mapped device memory and return to the caller.
				Marshal.Copy(
					_Device.GetNextPeakDataBuffer(),			// Source -- Shared device memory
				        _RawPeakData,					// Destination -- response
					0,						// Offset into source
				        (int) _Device.PeakDmaBufferSizeInBytes );	// Number of 

				// Point the response at the copied data
				responseBytes = _RawPeakData; 

				// Wahoo...it freaking worked...
				return CommandStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandStatus.ErrorProcessing;
			}
		}

		/// <summary>
		/// Gets the raw peak data as transferred by the hardware (FPGA).
		/// </summary>
		/// <returns>A status that indicates how the command exited.</returns>
		/// <param name="commandFields">Command fields.</param>
		/// <param name="responseBytes">Response bytes.</param>
		private static unsafe CommandStatus GetRawSpectra( string[] commandFields, out byte[] responseBytes )
		{
			try
			{
				// Copy the raw spectrum data from the mapped device memory and return to the caller.
				Marshal.Copy(
					_Device.GetNexSpectrumDataBuffer(),		// Source -- Shared device memory
					_RawSpectrumData,				// Destination -- response
					0,						// Offset into source
					(int) _Device.SpectrumDmaBufferSizeInBytes );	// Number of 

				// Point the response at the copied data
				responseBytes = _RawSpectrumData; 

				// Wahoo...it freaking worked...
				return CommandStatus.Success;
			}
			catch( Exception ex )
			{
				// Return exception message
				responseBytes = ASCIIEncoding.ASCII.GetBytes( ex.Message );

				return CommandStatus.ErrorProcessing;
			}
		}
	}
}
