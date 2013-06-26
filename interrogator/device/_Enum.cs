using System;

namespace MicronOptics.Hyperion.Interrogator.Device
{
	[Flags]
	public enum DmaModes : ulong
	{
		Off = 0x0,
		Peak = 0x2,
		Spectrum = 0x4,
		All = Peak | Spectrum
	}

	[Flags]
	public enum Interrupts : ulong
	{
		Off = 0x0,
		Peak = 0x2,
		Spectrum = 0x4,
		All = Peak | Spectrum
	}
}

