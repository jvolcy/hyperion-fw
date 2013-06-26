using System;

namespace MicronOptics.Hyperion.Interrogator.Device
{
	/// <summary>
	/// The MMapDmaBufferOffset represents the valid offset that can be passed
	/// to SetMMapBufferOffset command. The offsets are used when the process is initializing
	/// the memory map between the hardware and the software process (virual memory) in which
	/// the interface code is executing.
	/// </summary>
	internal static class MMapDmaBufferOffset
	{
		internal const int Peak = 0x00000000;
		internal const uint Spectrum = 0x80000000;
	}
	/// <summary>
	/// The DeviceRegisterAddress represents the valid register addresses exposed
	/// by the underlying device FPGA hardware.
	/// </summary>
	internal static class DeviceRegisterAddress
	{
		public const uint FpgaVersion = 0x0001;
		public const uint DmaEnable = 0x000C;
		public const uint SpectrumDmaBufferCount = 0x0031;
		public const uint SpectrumDmaBufferSizeInBytes = 0x0032;
		public const uint PeakDmaBufferCount = 0x0033;
		public const uint PeakDmaBufferSizeInBytes = 0x0034;
		public const uint SystemConfiguration = 0x0080;
		public const uint InterruptEnable = 0x0100;
	}

//	/// <summary>
//	/// The DmaModes control which data types are transferred from the device
//	/// into the PC using Direct Memory Access (DMA) transfer.
//	/// </summary>
//	internal static class DeviceDmaModes
//	{
//		public const uint Off = 0x0;
//		public const uint Peak = 0x2;
//		public const uint Spectrum = 0x4;
//	}
//
//	/// <summary>
//	/// The InterruptModes control which data types in the device signal the PC
//	/// when new data is available.
//	/// </summary>
//	internal static class DeviceInterruptModes
//	{
//		public const uint Off = 0x0;
//		public const uint Peak = 0x2;
//		public const uint Spectrum = 0x4;
//	}
}

