/* ===========================================================================
 Csm500DevCtrl.h
 sm500 device controls class implementation
 
 The driver control class inherits from the Csm500DriverInterface class.
 This class is intended to mechanically operate the sm500 device.
 Functions like enabling/disabling DMA, enabling/disabling interrupts,
 retrieving data, manually setting bias, scan amp, turning on/off
 auto-bias, setting P/D parameters and accessing other specific
 registers in the FPGA should be implemented here.  Intelligently
 operating the device (implementing PC-side laser controls, doing a 
 distance measurement, or any sort of data processing/interpretation)
 should be done in a derived class. This class in intended to be
 unintelligent.

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */
#include <iostream>
#include <string>

using namespace std;

#include <stdio.h>
#include <sys/ioctl.h>
#include <errno.h>
#include <string>
//#include <sys/time.h>   //for usleep()
#include "Csm500DevCtrl.h"
#include "sm500_common.h"


/* ===========================================================================
Csm500DevCtrl constructor
=========================================================================== */
Csm500DevCtrl::Csm500DevCtrl()
{
	bOpen = false;
}


/* ===========================================================================
destructor
=========================================================================== */
Csm500DevCtrl::~Csm500DevCtrl()
{
	Close();
}


/* ===========================================================================
Init() Initializes the driver through the default device node and
starts data acquisition
=========================================================================== */
void Csm500DevCtrl::Init()
{
	try
	{
		//invoke the base class Init() function
		Csm500DriverInterface::Init();
	}
	catch (int err)
	{
		bOpen = false;
    	throw (err);	//rethrow any returned error
	}
	
	bOpen = true;
	SM500_DBG( cout<<"Enabling Ints...\n"; );
	EnableInterrupts(SM500_INT_PK + SM500_INT_FS);
	EnableDma(SM500_DMA_PK + SM500_DMA_FS);
}


/* ===========================================================================
This overloaded version of Init() initializes the driver through a
specified device node and start data acquisition
=========================================================================== */
void Csm500DevCtrl::Init(const char* DevNode)
{
	try
	{
		//invoke the base class Init() function
		Csm500DriverInterface::Init(DevNode);
	}
	catch (int err)
	{
		bOpen = false;
    	throw (err);	//rethrow any returned error
	}

	EnableInterrupts(SM500_INT_PK + SM500_INT_FS);
	EnableDma(SM500_DMA_PK + SM500_DMA_FS);
}


/* ===========================================================================
stops the data acquisition process and closes the driver
=========================================================================== */
void Csm500DevCtrl::Close()
{
	if (!bOpen) return;		//dev not opened; nothing to do
	
	EnableInterrupts(SM500_INT_CLEAR);
	EnableDma(SM500_DMA_CLEAR);
	
	//invoke the base class Close() function
   	Csm500DriverInterface::Close();
	
	bOpen = false;
	
}


/* ===========================================================================
Returns a string representation of the currently running HDL version
=========================================================================== */
const char* Csm500DevCtrl::GetHdlVersion(void )
{
  union
  {
    uint32_t hdl_u32;
    char hdl_str[sizeof(uint32_t)];
  }hdl_ver;
  
  hdl_ver.hdl_u32 = ReadReg32(SM500_REG_HVER);  //read the u32 representation of the hdl version #
  
  for (int i=0; i<sizeof(uint32_t); i++)
    HdlVersion[i] = hdl_ver.hdl_str[sizeof(uint32_t)-1 - i];    //reverse the string to correct for endian mis-match
    
  HdlVersion[sizeof(uint32_t)] = 0;  //add a termination character
  return (const char*)HdlVersion;   //return a constant pointer
}


/* ===========================================================================
Returns a pointer to the next DMAed peaks data buffer.
This is a blocking call.
=========================================================================== */
const void* Csm500DevCtrl::GetPeaksData(void)
{
  uint16_t val;
  
  if ( ioctl(fd, SM500_IOC_GET_PEAKS_DATA, (unsigned long)(&val)) == -1)
    throw errno;
    
  return DmaPeaksBuffer[val];
}


/* ===========================================================================
Returns a pointer to the next DMAed FS data buffer.
This is a blocking call.
=========================================================================== */
const void* Csm500DevCtrl::GetFsData(void)
{
  uint16_t val;
  
  if ( ioctl(fd, SM500_IOC_GET_SPECTRUM, (unsigned long)(&val)) == -1)
    throw errno;
    
  return DmaFsBuffer[val];
}


/* ===========================================================================
Returns true if a call to GetPeaksData() would not block; false otherwise
=========================================================================== */
bool Csm500DevCtrl::PeaksDataReady(void)
{
	uint8_t val;
	
  if ( ioctl(fd, SM500_IOC_PEAKS_DATA_READY, (unsigned long)(&val) ) == -1)
    throw errno;
    
  return val ? true : false;
}


/* ===========================================================================
Returns true if a callto GetFsData() would not block; false otherwise
=========================================================================== */
bool Csm500DevCtrl::FsDataReady(void)
{
	uint8_t val;
	
  if ( ioctl(fd, SM500_IOC_FS_DATA_READY, (unsigned long)(&val) ) == -1)
    throw errno;
    
  return val ? true : false;
}


/* ===========================================================================
cancels all read requests (releases all blocked readers)
=========================================================================== */
void Csm500DevCtrl::CancelReads(void)
{
  if ( ioctl(fd, SM500_IOC_CANCEL_READ) == -1 )
    throw errno;
}


/* ===========================================================================
use this to achieve a specific, non-default DMA behavior
=========================================================================== */
void Csm500DevCtrl::EnableDma(uint32_t DmaEnableFlag)
{
	WriteReg32(SM500_REG_DMACR, DmaEnableFlag);
}


/* ===========================================================================
use this to achieve a specific, non-default interrupt behavior
=========================================================================== */
void Csm500DevCtrl::EnableInterrupts(uint32_t IntEnableFlag)
{
	WriteReg32(SM500_REG_INTE, IntEnableFlag);
}


/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
