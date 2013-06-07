/* ===========================================================================
 Csm500DevCtrl.h
 sm500 device controls class

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

#ifndef CSM500DEVCTRL_H
#define CSM500DEVCTRL_H

#include <iostream>
#include <string>

using namespace std;

#include <stdint.h>
#include "Csm500DriverInterface.h"

/* ===========================================================================
Constants
=========================================================================== */


/* ===========================================================================
Defaults
=========================================================================== */


/* ===========================================================================
Csm500DevCtrl class definition
=========================================================================== */
class Csm500DevCtrl:public Csm500DriverInterface
{
  public:
    //----------  ----------
    Csm500DevCtrl();                        //constructor
    virtual ~Csm500DevCtrl();               //destructor
    virtual void Init();                    //intialize the driver through the default device node and start data acquisition
    virtual void Init(const char* DevNode); //initialize the driver through a non-default device node and start data acquisition
    virtual void Close();                   //stops the data acquisition process and closes the driver
    virtual const char* GetHdlVersion(void);//returns the HDL version #
    virtual const void* GetPeaksData(void); //returns a pointer to the next DMAed peaks data buffer
    virtual const void* GetFsData(void);    //returns a pointer to the next DMAed FS data buffer
    virtual bool PeaksDataReady(void);  		//returns true if a call to GetPeaksData() would not block; false otherwise
    virtual bool FsDataReady(void);     		//returns true if a callto GetFsData() would not block; false otherwise
    virtual void CancelReads(void);         //cancels all read requests (releases blocked readers)

  protected:
  	bool bOpen;															//true when the device is successfully opened; false otherwise
    char HdlVersion[sizeof(uint32_t)+1];    //size of u32 plus string terminating character
    virtual void EnableDma(uint32_t DmaEnableFlag);            //use this to achieve a specific, non-default DMA behavior
    virtual void EnableInterrupts(uint32_t IntEnableFlag);     //use this to achieve a specific, non-default interrupt behavior


};

#endif // #ifndef CSM500DEVCTRL_H



/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
