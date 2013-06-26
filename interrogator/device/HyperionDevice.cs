using System;
using System.Runtime.InteropServices;

namespace MicronOptics.Hyperion.Interrogator.Device
{
	/// <summary>
	/// The HyperionDevice class provides high level access to the interrogator hardware (ie Set/Get Bias, 
	/// Get Wavelength Range, etc).
	/// </summary>
	public class HyperionDevice : IDisposable
	{
		#region -- Instance Variables --

		IDeviceInterface _deviceInterface;

		private IntPtr[] _PeakDataBuffers;
		private IntPtr[] _SpectrumDataBuffers;

		private static byte[] _rawPeakData;
		private static byte[] _rawSpectrumData;

		#endregion


		#region -- Constructors --

		public HyperionDevice( IDeviceInterface deviceInterface )
		{
			_deviceInterface = deviceInterface;

			// Map device memory into the process virtual memory for peak/spectrum data.
			InitializeDataBuffers();

			// The device hardware (FPGA) specifies to the device driver the size and number of peak/spectrum
			// DMA buffers. The size of the DMA buffers is needed to allocate the managed memory for returning
			// in response to GetRawPeakData/GetRawSpectrumData commads.
			_rawPeakData = new byte[ PeakDmaBufferSizeInBytes ];
			_rawSpectrumData = new byte[ SpectrumDmaBufferSizeInBytes ];

			// Enable Data Acquisition from the device
			ConfigureInterrupts( Interrupts.All );
			ConfigureDMA( DmaModes.All );
		}

		#endregion


		#region -- Public Properties --

		/// <summary>
		/// Gets the raw peak data dma buffer size in bytes (specified by the hardware/FPGA).
		/// </summary>
		/// <value>The raw peak data dma buffer size in bytes.</value>
		public uint PeakDmaBufferSizeInBytes
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the raw peak data dma buffer count (specified by the hardware/FPGA).
		/// </summary>
		/// <value>The raw peak data dma buffer count.</value>
		public uint PeakDmaBufferCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the raw spectrum data dma buffer size in bytes (specified by the hardware/FPGA).
		/// </summary>
		/// <value>The raw spectrum data dma buffer size in bytes.</value>
		public uint SpectrumDmaBufferSizeInBytes
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the raw spectrum data dma buffer count (specified by the hardware/FPGA).
		/// </summary>
		/// <value>The raw spectrum data dma buffer count.</value>
		public uint SpectrumDmaBufferCount
		{
			get;
			private set;
		}

		#endregion


		#region -- Private Methods --

		/// <summary>
		/// Map the peak/spectrum device data into the process virtual memory.
		/// </summary>
		private unsafe void InitializeDataBuffers()
		{
			// Retrieve the Peak Data Buffer setup from the device hardware
			PeakDmaBufferCount = _deviceInterface.ReadRegister( DeviceRegisterAddress.PeakDmaBufferCount );
			PeakDmaBufferSizeInBytes = _deviceInterface.ReadRegister( DeviceRegisterAddress.PeakDmaBufferSizeInBytes );

			// Create storage for peak data pointers
			_PeakDataBuffers = new IntPtr[ PeakDmaBufferCount ];

			for( uint index=0; index<PeakDmaBufferCount; index++ )
			{
				// Memory Map Peak Buffers to virtual memory for this process
				_PeakDataBuffers[ index ] = _deviceInterface.GetMemoryMappedBuffer( 
					MMapDmaBufferOffset.Peak + index,
					PeakDmaBufferSizeInBytes );
			}

			// Retrieve the Full Spectrum Data Buffer setup from the device hardware
			SpectrumDmaBufferCount = _deviceInterface.ReadRegister( DeviceRegisterAddress.SpectrumDmaBufferCount );
			SpectrumDmaBufferSizeInBytes = _deviceInterface.ReadRegister( DeviceRegisterAddress.SpectrumDmaBufferSizeInBytes );

			// Create storage for peak data pointers
			_SpectrumDataBuffers = new IntPtr[ SpectrumDmaBufferCount ];

			for( uint index = 0; index < SpectrumDmaBufferCount; index++ )
			{
				_SpectrumDataBuffers[ index ] = _deviceInterface.GetMemoryMappedBuffer( 
					MMapDmaBufferOffset.Spectrum + index,
					SpectrumDmaBufferSizeInBytes );
			}
		}

		/// <summary>
		/// Enable/Disable device interrupts.
		/// </summary>
		/// <param name="interrupts">Specifies which device events should produce interrupts.</param>
		public void ConfigureInterrupts( Interrupts interrupts )
		{
			// Enable Interrupts
			_deviceInterface.WriteRegister(
				DeviceRegisterAddress.InterruptEnable, 
				(uint) interrupts );
		}

		/// <summary>
		/// Enable/Disable DMA transfer of device data.
		/// </summary>
		/// <param name="dma">Specifies which data types are transferred from the device into the 
		/// PC memory.</param>
		public void ConfigureDMA( DmaModes dmaModes )
		{
			// Enable Interrupts
			_deviceInterface.WriteRegister(
				DeviceRegisterAddress.DmaEnable, 
				(uint) dmaModes );
		}

		#endregion


		#region -- Public Methods --

		/// <summary>
		/// Read value of the specified hardware register.
		/// </summary>
		/// <returns>The value stored in the requested device register.</returns>
		/// <param name="address">The location of the device register.</param>
		public uint ReadRegister( uint address )
		{
			return _deviceInterface.ReadRegister( address );
		}

		/// <summary>
		/// Write a value to the specified device register.
		/// </summary>
		/// <param name="address">The location of the device register.</param>
		/// <param name="value">The value to write to the device register.</param>
		public void WriteRegister( uint address, uint value )
		{
			_deviceInterface.WriteRegister( address, value );
		}

		/// <summary>
		/// Get the version of the FPGA code.
		/// </summary>
		/// <returns>The fpga version.</returns>
		public uint GetFpgaVersion()
		{
			return ReadRegister( DeviceRegisterAddress.FpgaVersion );
		}

		/// <summary>
		/// Get the version of the installed linux device driver.
		/// </summary>
		/// <returns>The device driver version.</returns>
		public uint GetDeviceDriverVersion()
		{
			return _deviceInterface.GetDeviceDriverVersion();
		}

		public byte[] GetRawPeakData()
		{
			// Copy the raw peak data from the mapped device memory and return to the caller.
			IntPtr peakDataBuffer = _PeakDataBuffers[ _deviceInterface.GetNextPeakDataBufferIndex() ];

			Marshal.Copy(
				peakDataBuffer,				// Source -- Shared device memory
				_rawPeakData,				// Destination -- response
				0,					// Offset into source
				(int) PeakDmaBufferSizeInBytes );	// Number of bytes

			return _rawPeakData;
		}

		public byte[] GetRawSpectrumData()
		{
			// Copy the raw spectrum data from the mapped device memory and return to the caller.
			IntPtr spectrumDataBuffer = _SpectrumDataBuffers[ _deviceInterface.GetNextSpectrumDataBufferIndex() ];

			Marshal.Copy(
				spectrumDataBuffer,			// Source -- Shared device memory
				_rawSpectrumData,			// Destination -- response
				0,					// Offset into source
				(int) SpectrumDmaBufferSizeInBytes );	// Number of bytes

			return _rawSpectrumData;
		}

		#endregion

		#region IDisposable implementation

		public void Dispose()
		{
			// Disable Data Acquistion from the device.
			ConfigureDMA( DmaModes.Off );
			ConfigureInterrupts( Interrupts.Off );

			// Cleanup up unmanaged device resources
			_deviceInterface.Close();
		}

		#endregion
	}
}

