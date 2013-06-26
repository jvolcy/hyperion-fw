using System;

namespace MicronOptics.Hyperion.Interrogator.Device
{
	public interface IDeviceInterface
	{
		IntPtr GetMemoryMappedBuffer( uint index, ulong length );

		uint ReadRegister( uint address );
		void WriteRegister( uint address, uint value );

		uint GetNextPeakDataBufferIndex();
		uint GetNextSpectrumDataBufferIndex();

		uint GetDeviceDriverVersion();

		void Close();
	}
}

