using System;
using System.Runtime.InteropServices;
using Mono.Unix.Native;

namespace MicronOptics.Hyperion.Interrogator.Device
{
	public unsafe class HardwareDevice
	{
		#region -- Constants --

		private const string _DevicePath = @"/dev/sm500";

		/// <summary>
		/// The IoctlCommand class contains the valid device driver ioctl command codes that are
		/// supported by the hyperion interrogator.
		/// </summary>
		private static class IoctlRequestCodes
		{
			#region -- Constants --

			private const int IoctlMagicNumber = 0xEB;
			private const int IoctlBaseNumber = 0xDD;
			/// <summary>
			/// Represents the valid IOCTL command types.
			/// </summary>
			private static class IoctlCommandType
			{
				internal const string IO = "IO";
				internal const string IOR = "IOR";
				internal const string IOW = "IOW";
				internal const string IOWR = "IOWR";
			}
			/// <summary>
			/// Represents the valid IOCTL command data types.
			/// </summary>
			private static class IoctlDataType
			{
				internal const string NULL = "NULL";
				internal const string CHAR = "CHAR";
				internal const string UCHAR = "CHAR";
				internal const string SHORT = "SHORT";
				internal const string USHORT = "SHORT";
				internal const string INT = "INT";
				internal const string UINT = "INT";
				internal const string LONG = "LONG";
				internal const string ULONG = "LONG";
			}
			#endregion

			#region -- External Methods --

			/// <summary>
			/// Build the request code (using C macros) for IOCTL device calls.
			/// </summary>
			/// <param name="type">Specifies how data flows to/from the device (read/write/etc).</param>
			/// <param name="magic_number">Ask a hippie on this one...I have no clue.</param>
			/// <param name="number">Vendor chose base command value. All values for specific commands
			/// are added to this value.</param>
			/// <param name="data_type">The data type passed to/from the IOCTL call.</param>
			[DllImport ("libbuild_ioctl_number.so", EntryPoint= "build_ioctl")]
			static extern uint BuildIoctlRequestCode( string type, int magic_number, int number, string data_type );
			#endregion

			#region -- Static Constructor --

			/// <summary>
			/// Using the C wrapper, initialize the IOCTL command codes to the values that the device
			/// driver is expecting.
			/// </summary>
			static IoctlRequestCodes()
			{
				// Driver Version
				GetDriverVersion = BuildIoctlRequestCode(
					IoctlCommandType.IOR, 
					IoctlMagicNumber, 
					IoctlBaseNumber + 0, 
					IoctlDataType.INT );

				// 32-bit Read Register
				ReadRegister32 = BuildIoctlRequestCode(
					IoctlCommandType.IOR, 
					IoctlMagicNumber, 
					IoctlBaseNumber + 3, 
					IoctlDataType.ULONG );

				// 32-bit Write Register
				WriteRegister32 = BuildIoctlRequestCode(
					IoctlCommandType.IOW, 
					IoctlMagicNumber, 
					IoctlBaseNumber + 6, 
					IoctlDataType.ULONG );

				// Set MMAP Buffer Index
				SetMMapIndex = BuildIoctlRequestCode(
					IoctlCommandType.IOW, 
					IoctlMagicNumber, 
					IoctlBaseNumber + 7, 
					IoctlDataType.INT );

				// Get Peaks
				GetPeakBufferIndex = BuildIoctlRequestCode(
					IoctlCommandType.IOR, 
					IoctlMagicNumber, 
					IoctlBaseNumber + 8, 
					IoctlDataType.INT );

				// Get Full Spectrum
				GetSpectrumBufferIndex = BuildIoctlRequestCode(
					IoctlCommandType.IOR, 
					IoctlMagicNumber, 
					IoctlBaseNumber + 10, 
					IoctlDataType.INT );
			}
			#endregion

			internal static readonly uint GetDriverVersion;
			internal static readonly uint ReadRegister32;
			internal static readonly uint WriteRegister32;
			internal static readonly uint GetPeakBufferIndex;
			internal static readonly uint SetMMapIndex;
			internal static readonly uint GetSpectrumBufferIndex;
		}
		/// <summary>
		/// The MMapDmaBufferOffset represents the valid offset that can be passed
		/// to SetMMapBufferOffset command. The offsets are used when the process is initializing
		/// the memory map between the hardware and the software process (virual memory) in which
		/// the interface code is executing.
		/// </summary>
		private static class MMapDmaBufferOffset
		{
			internal const int Peak = 0x00000000;
			internal const uint Spectrum = 0x80000000;
		}
		#endregion

		#region -- Instance Variables --

		private int _FileDescriptor;
		private byte*[] _PeakDataBuffers;
		private byte*[] _SpectrumDataBuffers;
		#endregion


		#region -- External Methods --

		[DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
		static extern private int IOCTL( int fd, uint request, void* data );

		[DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
		static extern private int IOCTL_IN( int fd, uint request, uint data );
		#endregion

		#region -- Static Methods --

		public static HardwareDevice Create()
		{
			// Attempt to create and Initialize a new HardwareDevice. By using Create
			// the ability to create and later attempt to use a non-functioning 
			// instance is avoided.
			HardwareDevice hardwareDevice = new HardwareDevice();
			hardwareDevice.Initialize();

			return hardwareDevice;
		}
		#endregion

		#region -- Private Methods --

		/// <summary>
		/// Open the device and initialize by mapping the driver memory into the process,
		/// enable interrupts, start dma transfers for both peak and full spectrum data.
		/// </summary>
		private int Initialize()
		{
			// Attempt to open the device and, if successful, store the resulting file
			// descriptor in the class variable.
			_FileDescriptor = Open();

			// Setup memory mapped data buffers
			InitializeDataBuffers();

			// Enable Interrupts
			WriteRegister(
				DeviceRegisterAddress.InterruptEnable, 
				DeviceInterruptModes.Peak | DeviceInterruptModes.Spectrum );

			// Enable DMA
			WriteRegister(
				DeviceRegisterAddress.DmaEnable, 
				DeviceDmaModes.Peak | DeviceDmaModes.Spectrum );

			return _FileDescriptor;
		}
		/// <summary>
		/// Open the device.
		/// </summary>
		/// <returns>The system level file descriptor for the device.</returns> 
		private int Open()
		{
			int fileDescriptor = Syscall.open( _DevicePath, OpenFlags.O_RDWR );

			// Open Device File
			if( _FileDescriptor < 0 )
			{
				throw new Exception(
					"Error opening the device: " +
					Syscall.GetLastError() );
			}

			return fileDescriptor;
		}
		/// <summary>
		/// Initialize by opening the device and preparing the memory mapped pointers.
		/// </summary>
		private unsafe void InitializeDataBuffers()
		{
			// Retrieve the Peak Data Buffer setup from the device hardware
			int peakDmaBufferCount = (int) ReadRegister( DeviceRegisterAddress.PeakDmaBufferCount );
			UInt64 peakDmaBufferSizeInBytes = ReadRegister( DeviceRegisterAddress.PeakDmaBufferSizeInBytes );

			// Create storage for peak data pointers
			_PeakDataBuffers = new byte*[ peakDmaBufferCount ];

			// Memory Map Peak Buffers to virtual memory for this process
			SetMMapIndex( MMapDmaBufferOffset.Peak );

			for( int index=0; index<peakDmaBufferCount; index++ )
			{
				_PeakDataBuffers[ index ] = (byte*) Syscall.mmap(
					IntPtr.Zero, 
					peakDmaBufferSizeInBytes,
					MmapProts.PROT_READ,
					MmapFlags.MAP_FILE | MmapFlags.MAP_SHARED,
					_FileDescriptor, 
					0 );

				// Check for Error
				if( (IntPtr) _PeakDataBuffers[ index ] == Syscall.MAP_FAILED )
				{
					throw new Exception(
						"Error creating peak data memory map: " +
						Syscall.GetLastError() );
				}
			}

			// Retrieve the Full Spectrum Data Buffer setup from the device hardware
			int spectrumDmaBufferCount = (int) ReadRegister( DeviceRegisterAddress.SpectrumDmaBufferCount );
			UInt64 spectrumDmaBufferSizeInBytes = ReadRegister( DeviceRegisterAddress.SpectrumDmaBufferCount );

			// Create storage for peak data pointers
			_SpectrumDataBuffers = new byte*[ spectrumDmaBufferCount ];

			// Memory Map Full Spectrum Buffers to virtual memory for this process
			SetMMapIndex( MMapDmaBufferOffset.Spectrum );

			for( int index=0; index<spectrumDmaBufferCount; index++ )
			{
				_SpectrumDataBuffers[ index ] = (byte*) Syscall.mmap(
					IntPtr.Zero, 
					spectrumDmaBufferSizeInBytes,
					MmapProts.PROT_READ,
					MmapFlags.MAP_FILE | MmapFlags.MAP_SHARED,
					_FileDescriptor, 
					0 );

				// Check for Error
				if( (IntPtr) _SpectrumDataBuffers[ index ] == Syscall.MAP_FAILED )
				{
					throw new Exception(
						"Error creating full spectrum memory map: " +
						Syscall.GetLastError() );
				}
			}
		}
		/// <summary>
		/// Sets the index of the memory map pointer in the device driver.
		/// </summary>
		/// <param name="index">Index.</param>
		private void SetMMapIndex( uint index )
		{
			if( IOCTL_IN( _FileDescriptor, IoctlRequestCodes.SetMMapIndex, index ) < 0 )
			{
				throw new Exception(
					"Error setting mmap index: " +
					Syscall.GetLastError() );
			}
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
			// The device driver expects the register address in the lower 32-bits
			// and returns the value in the upper 32-bits.
			ulong ioctlRegisterData = address;

			if( IOCTL( _FileDescriptor, IoctlRequestCodes.ReadRegister32, &ioctlRegisterData ) < 0 )
			{
				throw new Exception(
					"Error reading from device register: " +
					Syscall.GetLastError() );
			}
			else
			{
				return (uint) ( ioctlRegisterData >> 32 );
			}
		}
		/// <summary>
		/// Write a value to the specified device register.
		/// </summary>
		/// <param name="address">The location of the device register.</param>
		/// <param name="value">The value to write to the device register.</param>
		public void WriteRegister( uint address, uint value )
		{
			// The device driver expects the register address in the lower 32-bits
			// and returns the value in the upper 32-bits.
			ulong ioctlRegisterData = ( (ulong) value << 32 ) + address;

			if( IOCTL( _FileDescriptor, IoctlRequestCodes.WriteRegister32, &ioctlRegisterData ) < 0 )
			{
				throw new Exception(
					"Error writing to device register: " +
					Syscall.GetLastError() );
			} 
		}
		/// <summary>
		/// Gets the next available peak data buffer.
		/// </summary>
		/// <returns>A pointer to the first byte in the most recently transferred peak data
		/// buffer.</returns>
		/// <param name="_FileDescriptor">_FileDescriptor.</param>
		public byte* GetNextPeakBuffer()
		{
			// The GetPeakBufferIndex returns the index of the pointer that
			// was most recently refreshed by the device hardware.
			uint index;

			if( IOCTL( _FileDescriptor, IoctlRequestCodes.GetPeakBufferIndex, &index ) < 0 )
			{
				throw new Exception(
					"Error retrieving next peak buffer: " +
					Syscall.GetLastError() );
			}

			// Return the buffer pointer...not the index...so that external callers
			// do not need any knowledge of the array of buffers.
			return _PeakDataBuffers[ index ];
		}
		/// <summary>
		/// Gets the next available full spectrum data buffer.
		/// </summary>
		/// <returns>A pointer to the first byte in the most recently transferred full spectrum 
		/// data buffer.</returns>
		/// <param name="_FileDescriptor">_FileDescriptor.</param>
		public byte* GetNexSpectrumBuffer()
		{
			// The GetSpectrumBufferIndex returns the index of the pointer that
			// was most recently refreshed by the device hardware.
			uint index;

			if( IOCTL( _FileDescriptor, IoctlRequestCodes.GetSpectrumBufferIndex, &index ) < 0 )
			{
				throw new Exception(
					"Error retrieving next spectrum buffer: " +
					Syscall.GetLastError() );
			}

			// Return the buffer pointer...not the index...so that external callers
			// do not need any knowledge of the array of buffers.
			return _SpectrumDataBuffers[ index ];
		}
		/// <summary>
		/// Gets the device driver version.
		/// </summary>
		/// <returns>The device driver version.</returns>
		public uint GetDeviceDriverVersion()
		{
			uint version;

			// Driver Version
			if( IOCTL( _FileDescriptor, IoctlRequestCodes.GetDriverVersion, &version ) < 0 )
			{
				throw new Exception(
					"Error retrieving device driver version: " +
					Syscall.GetLastError() );
			}
			else
			{
				return version;
			}
		}
		/// <summary>
		/// Stop all interrupts and DMA and close the device.
		/// </summary>
		public void Close()
		{
			// Disable Interrupts
			WriteRegister(
				DeviceRegisterAddress.InterruptEnable, 
				DeviceInterruptModes.Off );

			// Disable DMA
			WriteRegister(
				DeviceRegisterAddress.DmaEnable, 
				DeviceDmaModes.Off );

			// Close
			if( Syscall.close( _FileDescriptor ) < 0 )
			{
				// Error Closing
				Console.WriteLine( "Close Error: " + Stdlib.GetLastError() );
			}
		}
		#endregion
	}
}

