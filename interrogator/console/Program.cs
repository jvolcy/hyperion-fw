using System;
using System.Text;
using System.Runtime.InteropServices;

using MicronOptics.Hyperion.Interrogator.Device;
using MicronOptics.Hyperion.Interrogator.Controller;

namespace MicronOptics.Hyperion.Interrogator.Server
{
	class MainClass
	{


		public static void Main( string[] args )
		{
			// A HardwareDevice provides communication to the device FPGA
			// through a kernel device driver.
			using( HyperionDevice device = HyperionDevice.Create( HardwareInterface.Create() ) )
			{
				#region -- Create and Initialize Command Manager --

				// Create a new command manager to receive and process incoming requests.
				TcpCommandServer server = TcpCommandServer.GetInstance();
				server.Start();

				// The HyperionController allows multiple interfaces to re-use the same code
				// for processing incoming interrogator related requests.
				HyperionController controller = HyperionController.Create( device, server );

				#endregion

				#region -- Display FPGA and Driver Info --

				// Provide an easy sign that the communication with the device
				// is functioning
				Console.WriteLine( string.Format( "  FPGA Version: {0}",
				                                 ASCIIEncoding.ASCII.GetString( BitConverter.GetBytes( device.GetFpgaVersion() ) ) ) );

				// Provide an easy sign that the communication with the device
				// is functioning
				Console.WriteLine( string.Format( "Driver Version: {0}",
				                                 device.GetDeviceDriverVersion() ) );

				Console.WriteLine( "----------------------" );

				#endregion

				// Wait for user input to exit gracefully 
				Console.ReadLine();

				// Before exiting, propertly shutdown the device to allow for easy
				// start and stop debugging (no reboot ).
				server.Stop();
			}
		}
	}
}
