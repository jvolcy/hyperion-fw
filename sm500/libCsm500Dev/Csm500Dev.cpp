/* ===========================================================================
 Csm500Dev.h
 sm500 device class implementation

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
#include "Csm500Dev.h"
#include "sm500_common.h"


/* ===========================================================================
Csm500DevCtrl constructor
=========================================================================== */
Csm500Dev::Csm500Dev()
{
	bOpen = false;
}


/* ===========================================================================
destructor
=========================================================================== */
Csm500Dev::~Csm500Dev()
{
	Close();
}


/* ===========================================================================
Init() Initializes the driver through the default device node and
starts data acquisition
=========================================================================== */
void Csm500Dev::Init()
{
	try
	{
		//invoke the base class Init() function
		Csm500DevCtrl::Init();
	}
	catch (int err)
	{
		bOpen = false;
    	throw (err);	//rethrow any returned error
	}
	
	bOpen = true;
}


/* ===========================================================================
This overloaded version of Init() initializes the driver through a
specified device node and start data acquisition
=========================================================================== */
void Csm500Dev::Init(const char* DevNode)
{
	try
	{
		//invoke the base class Init() function
		Csm500DevCtrl::Init(DevNode);
	}
	catch (int err)
	{
		bOpen = false;
   		throw (err);	//rethrow any returned error
	}
	
	bOpen = true;
}


/* ===========================================================================
stops the data acquisition process and closes the driver
=========================================================================== */
void Csm500Dev::Close()
{
	if (!bOpen) return;		//dev not opened; nothing to do
		
	//invoke the base class Close() function
   	Csm500DevCtrl::Close();

	bOpen = false;
	
}




/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
