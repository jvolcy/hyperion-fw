/* ===========================================================================
 Csm500DriverInterface.cpp
 sm500 driver interface class implementation file

 The driver interface class fascilitates communications to the driver.
 It provides fascilities for reading and writing I/O registers,
 manages the mapping of kernel memory to user space and handles driver
 specific operations (like fetching the driver version #).  This base
 class performs no operations that directly affects the sm500 device
 it is intended to be nothing more than the conduit through which a 
 user application communicates with the sm500 driver.  The class is
 nearly device agnostic.

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */
#include <iostream>
#include <string>

using namespace std;

#include <stdio.h>
#include <fcntl.h>    //for open()
#include <unistd.h>   //for close()
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <errno.h>
#include <string.h>   //for strerror
//#include <sys/time.h>   //for usleep()
#include "Csm500DriverInterface.h"
#include "sm500_common.h"

/* ===========================================================================
=========================================================================== */
Csm500DriverInterface::Csm500DriverInterface()
{
  fd = 0;   //set to 0 to indicate that the driver is not yet opened
  NumDmaFsBuffers = 0;
  NumDmaPeaksBuffers = 0;
}


/* ===========================================================================
=========================================================================== */
Csm500DriverInterface::~Csm500DriverInterface()
{
  Close();
}


/* ===========================================================================
Overloaded Init function with no device node specify.  Here, we will use
the default device node DEFAULT_DEV_NODE
=========================================================================== */
void Csm500DriverInterface::Init(void)
{
  Init(DEFAULT_DEV_NODE);
}


/* ===========================================================================
Open the driver through the specified device node.  An overloaded Init
function calls this function with the default device node DEFAULT_DEV_NODE.
=========================================================================== */
void Csm500DriverInterface::Init(const char* DevNode)
{
  fd = 0;  
  fd = open(DevNode, O_RDWR);
  
  if (fd == -1)
  {
    fd = 0;
    throw errno;
  }

  SetupMemoryMap();
}


/* ===========================================================================
This function unmaps any mapped kernel memory, then close the handle to
the driver device node.
=========================================================================== */
void Csm500DriverInterface::Close(void)
{
  if ((fd == 0) || (fd == -1)) return;    //nothing to do
    
  //unmap memory here
  ReleaseMemoryMap();
  
  close(fd);
  fd = 0;
}


/* ===========================================================================
Reads a value from an sm500 8-bit register.
=========================================================================== */
uint8_t Csm500DriverInterface::ReadReg8(uint32_t reg)
{
  struct sm500_ioctl_reg_arg arg;

  arg.reg = reg;

  if ( ioctl(fd, SM500_IOC_READ_REG8, (unsigned long)(&arg) ) == -1)
    throw errno;

  return (uint8_t)(arg.value);
}


/* ===========================================================================
Reads a value from an sm500 16-bit register.
=========================================================================== */
uint16_t Csm500DriverInterface::ReadReg16(uint32_t reg)
{
  struct sm500_ioctl_reg_arg arg;

  arg.reg = reg;

  if ( ioctl(fd, SM500_IOC_READ_REG16, (unsigned long)(&arg) ) == -1)
    throw errno;

  return (uint16_t)(arg.value);
}


/* ===========================================================================
Reads a value from an sm500 32-bit register.
=========================================================================== */
uint32_t Csm500DriverInterface::ReadReg32(uint32_t reg)
{
  struct sm500_ioctl_reg_arg arg;

  arg.reg = reg;

  if ( ioctl(fd, SM500_IOC_READ_REG32, (unsigned long)(&arg) ) == -1)
    throw errno;

  return (uint32_t)(arg.value);
}


/* ===========================================================================
Writes a value to the an sm500 8-bit register.
=========================================================================== */
void Csm500DriverInterface::WriteReg8(uint32_t reg, uint8_t value)
{
  struct sm500_ioctl_reg_arg arg;

  arg.reg = reg;
  arg.value = value;

  if ( ioctl(fd, SM500_IOC_WRITE_REG8, (unsigned long)(&arg) ) == -1 )
    throw errno;

}


/* ===========================================================================
Writes a value to the an sm500 16-bit register.
=========================================================================== */
void Csm500DriverInterface::WriteReg16(uint32_t reg, uint16_t value)
{
  struct sm500_ioctl_reg_arg arg;

  arg.reg = reg;
  arg.value = value;

  if ( ioctl(fd, SM500_IOC_WRITE_REG16, (unsigned long)(&arg) ) == -1)
    throw errno;

}


/* ===========================================================================
Writes a value to the an sm500 32-bit register.
=========================================================================== */
void Csm500DriverInterface::WriteReg32(uint32_t reg, uint32_t value)
{
  struct sm500_ioctl_reg_arg arg;

  arg.reg = reg;
  arg.value = value;

  if ( ioctl(fd, SM500_IOC_WRITE_REG32, (unsigned long)(&arg) ) == -1)
    throw errno;
}


/* ===========================================================================
Get Driver Version
=========================================================================== */
const char* Csm500DriverInterface::GetDriverVersion(void)
{
  uint32_t val;
  
  if ( ioctl(fd, SM500_IOC_DRV_VERSION, (unsigned long)(&val)) == -1)
    throw errno;

  sprintf(DriverVersion, "%d.%d", val>>16, val&0xFFFF);
  
  return (const char *)DriverVersion;
}


/* ===========================================================================
GetNumDmaPeakBuffers()
Returns the # of DMA peak buffers.
=========================================================================== */
int Csm500DriverInterface::GetNumDmaPeakBuffers(void )
{
  return ReadReg32(SM500_REG_NPKBUF);
}


/* ===========================================================================
GetDmaPeakBufferSize()
Returns the size of the individual DMA peak buffers.
=========================================================================== */
int Csm500DriverInterface::GetDmaPeakBufferSize(void )
{
  return ReadReg32(SM500_REG_PKBUFSZ);
}


/* ===========================================================================
GetNumDmaFsBuffers
Returns the # of DMA FS buffers
=========================================================================== */
int Csm500DriverInterface::GetNumDmaFsBuffers(void )
{
  return ReadReg32(SM500_REG_NFSBUF);
}


/* ===========================================================================
GetDmaFsBufferSize()
Returns the size of the individual DMA FS buffers.
=========================================================================== */
int Csm500DriverInterface::GetDmaFsBuffersize(void )
{
  return   ReadReg32(SM500_REG_FSBUFSZ);

}


/* ===========================================================================
    void **DmaPeaskBuffer;  //pointers to DMA peaks buffers
    void **DmaFsBuffer;     //pointers to DAM FS buffers
=========================================================================== */
void Csm500DriverInterface::SetupMemoryMap(void)
{
  //---------- Setup Peaks memory map ----------
  NumDmaPeaksBuffers = GetNumDmaPeakBuffers();
  DmaPeaksBuffer = new void*[NumDmaPeaksBuffers];

  DmaPeaksBufferSize = GetDmaPeakBufferSize();

  //void all newly allocated pointers, in case mapping fails
  for (int i=0; i<NumDmaPeaksBuffers; i++)
    DmaPeaksBuffer[i] = MAP_FAILED;
  
  //Set the mmap index pointer to zero (Peaks)
  if ( ioctl(fd, SM500_IOC_SET_MMAP_INDEX, SM500_MMAP_PEAKS_BUFFER + 0) == -1)
    throw errno;
  
  //map the pointers (the kernel pointer auto-increments)
  for (int i=0; i<NumDmaPeaksBuffers; i++)
  {
      DmaPeaksBuffer[i] = (void*)mmap(0, DmaPeaksBufferSize, PROT_READ, MAP_FILE|MAP_SHARED, fd, 0);
      if (DmaPeaksBuffer[i] == MAP_FAILED)
      {
        SM500_DEBUG( cout<<"Failed to mmap DmaPeaksBuffer["<<i<<"].\n" );
        throw errno;
      }
      
      SM500_DEBUG( cout<<"Peak Buffer["<<i<<"] mapped at "<<hex<<(uint32_t)DmaPeaksBuffer[i]<<"\n" );
  }
  
  //---------- Setup FS memory map ----------
  NumDmaFsBuffers = GetNumDmaFsBuffers();
  DmaFsBuffer = new void*[NumDmaFsBuffers];
  
  DmaFsBufferSize = GetDmaFsBuffersize();
  
  //void all newly allocated pointers, in case mapping fails
  for (int i=0; i<NumDmaFsBuffers; i++)
    DmaFsBuffer[i] = MAP_FAILED;

  //Set the mmap index pointer to zero (FS)
  if ( ioctl(fd, SM500_IOC_SET_MMAP_INDEX, SM500_MMAP_FS_BUFFER + 0) == -1)
    throw errno;
  
  //map the pointers (the kernel pointer auto-increments)
  for (int i=0; i<NumDmaFsBuffers; i++)
  {
      DmaFsBuffer[i] = (void*)mmap(0, DmaFsBufferSize, PROT_READ, MAP_FILE|MAP_SHARED, fd, 0);
      if (DmaFsBuffer[i] == MAP_FAILED)
      {
        SM500_DBG(cout<<"Failed to mmap DmaFsBuffer["<<i<<"].\n";);
        throw errno;
      }
      SM500_DBG(cout<<"FS Buffer["<<i<<"] mapped at "<<hex<<(uint32_t)DmaFsBuffer[i]<<"\n";);
  }
  
}


/* ===========================================================================
=========================================================================== */
void Csm500DriverInterface::ReleaseMemoryMap(void )
{
  //---------- Unmap Peaks Buffers ----------
  for (int i=0; i<NumDmaPeaksBuffers; i++)
  {
    if (DmaPeaksBuffer[i] != MAP_FAILED)
      if (munmap(DmaPeaksBuffer[i], DmaPeaksBufferSize))
      {
        SM500_DEBUG(cout<<"Failed to unmap DmaPeaksBuffer["<<i<<"]:");
        SM500_DEBUG(cout<<strerror(errno)<<"\n");
        //throw errno;
      }

    DmaPeaksBuffer[i] = MAP_FAILED;
  }
    
  //---------- Unmap FS Buffers ----------
  for (int i=0; i<NumDmaFsBuffers; i++)
  {
    if (DmaFsBuffer[i] != MAP_FAILED)
      if (munmap(DmaFsBuffer[i], DmaFsBufferSize))
      {
        SM500_DEBUG(cout<<"Failed to unmap DmaFsBuffer["<<i<<"].\n");
        SM500_DEBUG(cout<<strerror(errno)<<"\n");
        //throw errno;
      }

    DmaFsBuffer[i] = MAP_FAILED;
  }
}


/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
