/* ===========================================================================
 sm500_public.h
 Public interfaces for sm500 driver.
 This header is intended to be shared by both the kernel driver and any
 user-space application that wishes to communicate with the driver.

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc. sm500.h
=========================================================================== */
#ifndef DRVSM500_PUBLIC_H
#define DRVSM500_PUBLIC_H

#ifndef __KERNEL__
#include <stdint.h>
#endif


/* ===========================================================================
	Misc. MMAP Constants
=========================================================================== */
/*  SM500_MMAPFS_BUFFER and SM500_MMAP_PEAKS_BUFFER are for use with the
SM500_IOC_SET_MMAP_INDEX ioctl from user space.
From user space: 
 - FS buffer memory should be specified as (SM500_MMAP_FS_BUFFER + N),
   where N is the FS buffer index.
 - Peaks buffer memory should be specified as (SM500_MMAP_PEAKS_BUFFER + N),
   where N is the peaks buffer index.  */
#define SM500_MMAP_FS_BUFFER				0x80000000
#define SM500_MMAP_PEAKS_BUFFER			0x0


/* ===========================================================================
	Misc. IOCTL argument structures
=========================================================================== */
/* structure for reading/writing sm500 register map. */
struct sm500_ioctl_reg_arg
  {
    uint32_t reg;
		uint32_t value;
	};


/* ===========================================================================
	IOCTLs
=========================================================================== */
#define SM500_IOC_MAGIC 0xeb
#define SM500_IOC_BASE  0x90+'M'

#define SM500_IOC_DRV_VERSION				_IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+0, int)

#define SM500_IOC_READ_REG8 				_IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+1, unsigned long)
#define SM500_IOC_READ_REG16				_IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+2, unsigned long)
#define SM500_IOC_READ_REG32				_IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+3, unsigned long)

#define SM500_IOC_WRITE_REG8				_IOW(SM500_IOC_MAGIC,SM500_IOC_BASE+4, unsigned long)
#define SM500_IOC_WRITE_REG16				_IOW(SM500_IOC_MAGIC,SM500_IOC_BASE+5, unsigned long)
#define SM500_IOC_WRITE_REG32				_IOW(SM500_IOC_MAGIC,SM500_IOC_BASE+6, unsigned long)

#define SM500_IOC_SET_MMAP_INDEX		_IOW(SM500_IOC_MAGIC,SM500_IOC_BASE+7,int)

#define SM500_IOC_GET_PEAKS_DATA		_IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+8, int)  //Get Peaks Data
#define SM500_IOC_PEAKS_DATA_READY  _IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+9, unsigned long)

#define SM500_IOC_GET_SPECTRUM			_IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+10, int)	//Get Raw Spectrum
#define SM500_IOC_FS_DATA_READY		  _IOR(SM500_IOC_MAGIC,SM500_IOC_BASE+11, unsigned long)

#define SM500_IOC_CANCEL_READ				_IO(SM500_IOC_MAGIC,SM500_IOC_BASE+12)		//Eventually, use this to cancel both peak and fs reads
/* Note that,  SM500_IOC_CANCEL_READ simply wakes up readers on both the peaks and fs wait queues. */



/* ===========================================================================
	sm500 Register Map
=========================================================================== */
#define SM500_REG_NULLR     0x00   // Null Register 
#define SM500_REG_HVER      0x01   // HDL Version Register 
#define SM500_REG_DMATAR0   0x02   // DMA Transfer Address Register 0
#define SM500_REG_DMATAR1   0x03   // DMA Transfer Address Register 1
#define SM500_REG_DMATAR2   0x04   // DMA Transfer Address Register 2
#define SM500_REG_DMATAR3   0x05   // DMA Transfer Address Register 3
#define SM500_REG_DMATAR4   0x06   // DMA Transfer Address Register 4
#define SM500_REG_DMATAR5   0x07   // DMA Transfer Address Register 5
#define SM500_REG_DMATAR6   0x08   // DMA Transfer Address Register 6
#define SM500_REG_DMATAR7   0x09   // DMA Transfer Address Register 7
#define SM500_REG_DMAXP     0x0A   // DMA Transfer Pointer 
#define SM500_REG_DMACNT    0x0B   // DMA Count Register 
#define SM500_REG_DMACR     0x0C   // DMA Control Register 
//#define SM500_REG_FCR       0x0D   // Flash Control Register 
//#define SM500_REG_FCOMR     0x14   // Flash Command Register
//#define SM500_REG_ADCCR     0x15   // ADC Control Register 
//#define SM500_REG_ADCWR     0x16   // ADC Data Write Register 
//#define SM500_REG_ADCRR     0x17   // ADC Data Read Register
//#define SM500_REG_CGCR      0x18   // Clock Generator Control Register
//#define SM500_REG_CGWR      0x19   // Clock Generator Data Write Register
//#define SM500_REG_CGRR      0x1A   // Clock Generator Data Read Register
//#define SM500_REG_BSDCR     0x1B   // Bias DAC Control Register
//#define SM500_REG_SCNCR     0x1C   // Scan DAC Control Register
//#define SM500_REG_SACR      0x1D   // Scan Amp DAC Control Register
//#define SM500_REG_SOACR     0x1E   // SOA DAC Control Register 
//#define SM500_REG_VOACR     0x1F   // VOA DAC Control Register 
//#define SM500_REG_TSCR      0x20   // Temperature Sensor Control Register 
//#define SM500_REG_TSDDR     0x21   // Temperature Sensor Data Read Register
//#define SM500_REG_LEDCR     0x22   // LED Control Register
//#define SM500_REG_TECCR     0x23   // TEC Control Register
//#define SM500_REG_USBCSR    0x24   // USB Port Control & Status Register 
//#define SM500_REG_USBWR     0x25   // USB Port Data Write Register 
//#define SM500_REG_USBRR     0x26   // USB Port Data Read Register 

#define SM500_REG_DMAFSAR 	0x30		//FS DMA Address
#define SM500_REG_NFSBUF    0x31    //# of DMA FS Buffers
#define SM500_REG_FSBUFSZ   0x32    //FS Buffer Size
#define SM500_REG_NPKBUF    0x33    //# of DMA Peaks Buffers
#define SM500_REG_PKBUFSZ   0x34    //Peaks Buffer Size
#define SM500_REG_TSOFST    0x35    //Timestamp Offset
#define SM500_REG_DMASNLO   0x36    //Low DWORD of S/N or most recently DMAed data set
#define SM500_REG_DMASNHI   0x37    //Hi DWORD of S/N or most recently DMAed data set

#define SM500_REG_SYSCON    0x80   // System Control Register
//#define SM500_REG_SYSREG    0x81   // Scan DAC Delay Register 
//#define SM500_REG_SYSREG1   0x82   // Scan DAC Skip Register
#define SM500_REG_INTE      0x100  // Interrupt ENABLE Register 
#define SM500_REG_INTF      0x101  // Interrupt FLAG Register 
#define SM500_REG_INTDR     0x102  // Interrupt Data Register 


// DMA Ctrl bits in SM500_REG_DMACR
#define SM500_DMA_CLEAR     0x0000    //Nothing
#define SM500_DMA_PK        0x0002    //Enable Peaks DMAs
#define SM500_DMA_FS        0x0004    //Enable FS DMAs


// interrupt bits in SM500_REG_INTE and SM500_REG_INTF
#define SM500_INT_CLEAR     0x0000    //Nothing
#define SM500_INT_PK        0x0002    //Peaks DMA Interrupt
#define SM500_INT_FS        0x0004    //FS DMA Interrupt
#define SM500_INT_FS_SET    (1<<31)   //FS Available for this Peaks DMA

// SYSCON register bits
#define SM500_SYSCON_SCANR  0x01       // Scan Run Bit
#define SM500_SYSCON_OMODE  0x02       // SM500 Operation Mode
#define SM500_SYSCON_SRESET 0x04       // Soft Reset for Peripherals
#define SM500_SYSCON_SINTR  0x08       // Scan Interrupt Reset
#define SM500_SYSCON_ISYNC  0x10       // Invert External Sync




#endif		//#ifndef DRVSM500_PUBLIC_H

