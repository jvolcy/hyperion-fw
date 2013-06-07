#include <string>
#include <iostream>

using namespace std;

#include <errno.h>
#include <string.h>

#include "Csm500Dev.h"

int main(int argc, char **argv) 
{
  
  Csm500Dev sm500;

  try
  {
  sm500.Init();

  cout <<"Driver Version is "<<sm500.GetDriverVersion()<<".\n";
  cout <<"HDL Version str is "<<sm500.GetHdlVersion()<<"\n";
  
  
  /* Test GetPeaksData() */  
  
  const uint32_t *peaks_data;  
  for (int i=0; i<10; i++)  // 10 acquistion = timestamp should increment by 0.01 second & expect to see ~ 10 interrupts
  {
    peaks_data = (const uint32_t*)sm500.GetPeaksData();
    for (int j=0; j<10; j++)
      cout<<hex<<"Peaks["<<i<<"]["<<j<<"] = "<<peaks_data[j]<<"\n";
  }


  /* Test GetFsData() */
//FS seems to be at 20hz  
/*
  const uint32_t *spectrum;  
  for (int i=0; i<10; i++)  // 10 acquisition = timestamp should increment by 1 second & expect to see ~ 1000 interrupts
  {
    spectrum = (const uint32_t*)sm500.GetFsData();
    for (int j=0; j<10; j++)
      cout<<"FS["<<i<<"]["<<j<<"] = "<<spectrum[j]<<"\n";
  }
*/


  sm500.Close();
  }
  catch (int e)
  {
    cout <<strerror(e);
  }
  return 0;
}

