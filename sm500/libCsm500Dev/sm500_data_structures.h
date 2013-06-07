/* ===========================================================================
 sm500_data_structures.h
 
 This file houses data structures that describe the format of the data DMAed
 from the FPGA.

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */
#ifndef SM500_DATA_STRUCTURE_H
#define SM500_DATA_STRUCTURE_H

#include <stdint.h>

struct dma_peaks_header
  {
    uint32_t header_data[256]; //<============== This is a temporary place holder
  };


struct dma_peaks_data
  {
    struct dma_peaks_header header;
    //uint32_t *data;
    uint32_t data[512]; //<============== This is a temporary value
  };


struct dma_fs_header
  {
    uint32_t header_data[256]; //<============== This is a temporary place holder
  };


struct dma_fs_data
  {
    struct dma_fs_header header;
    //uint16_t **data;
    uint16_t data[4][20000]; //<============== These are temporary values
  };


#endif    //#ifndef SM500_DATA_STRUCTURE_H