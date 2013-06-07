/* ===========================================================================
 Csm500Dev.h
 sm500 device class definition

 The Csm500Dev class inherits from Csm500DevCtrl and is intended to peform
 the higher level functions of the sm500.  Among other things, this class
 contains the intelligence to properly interpret the data retrieved from
 calls to the inherited GetFsData() and GetPeaksData() functions.  Wave-
 length calibration, distance measurement, data averaging, binning, and 
 other data-centric functions should be done here.
 
 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */

#ifndef CSM500DEV_H
#define CSM500DEV_H

#include <iostream>
#include <string>

using namespace std;

#include <stdint.h>
#include "Csm500DevCtrl.h"
//#include "sm500_data_structures.h"

/* ===========================================================================
Constants
=========================================================================== */


/* ===========================================================================
Defaults
=========================================================================== */


/* ===========================================================================
Csm500Dev class definition
=========================================================================== */
class Csm500Dev:public Csm500DevCtrl
{
  public:
    //----------  ----------
    Csm500Dev();                        //constructor
    virtual ~Csm500Dev();               //destructor
    virtual void Init();                    //intialize the driver through the default device node and start data acquisition
    virtual void Init(const char* DevNode); //initialize the driver through a non-default device node and start data acquisition
    virtual void Close();                   //stops the data acquisition process and closes the driver

  protected:
  	bool bOpen;								//true when the device is successfully opened; false otherwise


};

#endif // #ifndef CSM500DEV_H



/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
