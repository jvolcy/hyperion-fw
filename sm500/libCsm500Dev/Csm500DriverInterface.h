/* ===========================================================================
 Csm500DriverInterface.h
 sm500 driver interface class definition

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

#ifndef CSM500DRIVERINTERFACE_H
#define CSM500DRIVERINTERFACE_H

#include <iostream>
#include <string>

using namespace std;

#include <stdint.h>
#include "sm500_public.h"   //public sm500 driver header

/* ===========================================================================
=========================================================================== */
#define DEFAULT_DEV_NODE    "/dev/sm500"

class Csm500DriverInterface
{
  public:
    //----------  ----------
    Csm500DriverInterface();	//constructor
    virtual ~Csm500DriverInterface();	//destructor
    virtual void Init(const char *DevNode);
    virtual void Init(void);
    virtual void Close(void);
    virtual uint8_t ReadReg8(uint32_t reg);
    virtual uint16_t ReadReg16(uint32_t reg);
    virtual uint32_t ReadReg32(uint32_t reg);
    virtual void WriteReg8(uint32_t reg, uint8_t value);
    virtual void WriteReg16(uint32_t reg, uint16_t value);
    virtual void WriteReg32(uint32_t reg, uint32_t value);
    virtual const char* GetDriverVersion(void);
    virtual int GetNumDmaPeakBuffers(void);
    virtual int GetNumDmaFsBuffers(void);
    virtual int GetDmaPeakBufferSize(void);
    virtual int GetDmaFsBuffersize(void);

  protected:
    char DriverVersion[10];
    virtual void SetupMemoryMap(void);
    virtual void ReleaseMemoryMap(void);
    
    int NumDmaFsBuffers;
    int DmaFsBufferSize;
    void **DmaFsBuffer;     //pointers to DAM FS buffers
    
    int NumDmaPeaksBuffers;
    int DmaPeaksBufferSize;
    void **DmaPeaksBuffer;  //pointers to DMA peaks buffers
    
    int fd;		//driver file descriptor

    //----------  ----------
//    struct dma_peaks_data DmaPeaksData;
//    struct dma_fs_data DmaFsData;

};

#endif // #ifndef CSM500DRIVERINTERFACE_H



/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
