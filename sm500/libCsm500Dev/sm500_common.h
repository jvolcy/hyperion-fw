/* ===========================================================================
 common.h
 
 This file contains macros, constants, definitions, etc... that are common to
 multiple un-related classes.
 

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */

#ifndef SM500_COMMON_H
#define SM500_COMMON_H

#include <iostream>
#include <sstream>
#include <string>
using namespace std;

/* ===========================================================================
Constants
=========================================================================== */



/* ===========================================================================
Macros
=========================================================================== */
//---------- Debug Macro ---------- 
//Uncomment the line below to enable debugging mode
#define SM500_DEBUG
#ifdef SM500_DEBUG
#define SM500_DBG(...) do{ __VA_ARGS__ } while(0)
#else
#define SM500_DBG(...)
#endif


/* =========================================================================
Templates
========================================================================= */

//---------- Number to string template ----------

template <typename T>
  string NumberToString (T number)
  {
    ostringstream ss;
    ss << number;
    return ss.str();
  }

  
//---------- String to number template ----------
template <typename T>
  T StringToNumber(const string &s)
  {
    istringstream ss(s);
    T result;
    return ss >> result ? result : 0;
  }


#endif // #ifndef SM500_COMMON_H



//----------  ----------
//----------  ----------
