/* ===========================================================================
 sm500_private.h
 Private interfaces for sm500 driver

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */

#ifndef DRVSM500_PRIVATE_H
#define DRVSM500_PRIVATE_H

#include <linux/autoconf.h>
#include <linux/module.h>
#include <linux/cdev.h>
#include <linux/init.h>
#include <linux/pci.h>
#include <linux/fs.h>
#include <linux/errno.h>
#include <linux/wait.h>
#include "sm500_public.h"

/* ===========================================================================
Driver Version Number & Changelog
=========================================================================== */
/* The driver version# will specified as Major.Minor.
So, setting SM500_VERSION_MAJOR to 1 and SM500_VERSION_MINOR
to 18 results in a driver version # of 1.18. */
#define SM500_VERSION_MAJOR	0
#define SM500_VERSION_MINOR	51

#define SM500_VERSION_I ((SM500_VERSION_MAJOR<<16) + SM500_VERSION_MINOR)

//---------- Changelog ----------
/*
Version		Date					Description
v0.50		May 2013				Initial re-write of sm500 driver by Volcy
v0.51		May 31 2013				Added a spinlock to the ISR
*/

/* ===========================================================================
Misc. Constants
=========================================================================== */
#define SM500_NAME "sm500"

/* Set SM500_MAJOR to zero for auto major number assignment
 or set both MAJOR and MINOR to assign them here */
#define SM500_MAJOR 0
#define SM500_MINOR 0

#define SM500_MAXCARDS 1
//#define SM500_TLP_SIZE 128

#define SM500_MAX_NUM_CLIENTS		1		//The maximum # of user-side apps that can simultaneously open and access the driver


/* ===========================================================================
Macros
=========================================================================== */
//---------- Debug Macro ---------- 
//Uncomment the line below to enable debugging mode
#define SM500_DEBUG
#ifdef SM500_DEBUG
#define SM500_DBG(X) X
#else
#define SM500_DBG(X)
#endif

//---------- kernel messages ---------- 
#define MSG(string, args...) printk(KERN_DEBUG SM500_NAME":" string, ##args)



/* ===========================================================================
Globals
=========================================================================== */
extern struct dev_sm500 sm500;		//sm500 device context



/* ===========================================================================
Externals
=========================================================================== */
extern int sm500_ioctl(struct inode *inode, struct file *file, unsigned int cmd,
                        unsigned long arg); 



/* ===========================================================================
Misc. structures
=========================================================================== */

//---------- DMA buffers structure ---------- 
struct dma_buffer
{
  dma_addr_t bus_addr; // physical address
  void *kernel_addr;   // kernel logical address
};


/* ===========================================================================
sm500 device context structure definition
=========================================================================== */
struct dev_sm500
{
  struct pci_dev *dev;
  void __iomem *BAR0;		//base address register 0

  struct cdev sm500_cdev;

  int open_count;		//# of open user-side clients

  //---------- DMA buffers ---------- 
//  struct dma_buffer dma_peaks_buffer[SM500_NUM_PEAK_BUFFERS];		//eventually want to make this dynamic
//  struct dma_buffer dma_fs_buffer[SM500_NUM_FS_BUFFERS];		//eventually want to make this dynamic
  struct dma_buffer *dma_peaks_buffer;		//eventually want to make this dynamic
  struct dma_buffer *dma_fs_buffer;		//eventually want to make this dynamic
  uint32_t mmap_index;		//used to specify which buffer mmap() maps to user space

  uint32_t NumDmaPeaksBuffers;    //the # of DMA peaks buffers
  uint32_t NumDmaFsBuffers;       //the # of FS buffers
  uint32_t DmaPeaksBufferSize;    //the size of an individual peaks DMA buffer
  uint32_t DmaFsBufferSize;       //the size of an individual FS DMA buffer

  uint16_t DmaBufferSnOffset32;   //the 32-bit offset for the kernel S/N in the DMA buffers (FS and Peaks)

  //---------- Wait queues ---------- 
  wait_queue_head_t fs_wq;	//sm500 full spectrum wait queue
  wait_queue_head_t peaks_data_wq;	//sm500 peaks data wait queue
  uint8_t bWaitQueueInitialized;	//set to 1 after the WQs have been successfully initialized

  //---------- wait queue flags ---------- 
  /*  Readers are put on a corresponding wait queue if the flags below are
  zero when a user-side reader wants data. */
  uint8_t fs_data_ready;	//flag set to 1 when new FS data has been DMAed.  Set to zero after user app reads the data
  uint8_t peaks_data_ready;  //flag set to 1 when new peaks data has been DMAed.  Set to zero after user app reads the data

  //---------- buffer pointers ---------- 
  /* For peaks and FS pointers: Ideally, the read buffer trails the write buffer
  by 1.  When the 2 are equal, readers are put on a wait queue. */

  uint16_t peaks_buf_wr_ptr;      //peaks buffer write pointer (the next location for writing = points to the oldest data set)
  uint16_t peaks_buf_rd_ptr;      //peaks buffer read pointer (the next location for reading = points to the newst data set)
  uint16_t fs_buf_wr_ptr;         //fs buffer write pointer
  uint16_t fs_buf_rd_ptr;         //fs buffer read pointer

  uint8_t target_pk_buf_index;    /* The index of the next peaks buffer to receive
                                  DMAed data.  This value is derived from the SN register
                                  and is where the peaks wr pointer needs to get to. */

  uint32_t fs_timestamp_sec;      //seconds portion of the FS timestamp
  uint32_t fs_timestamp_nsec;     //nano-seconds portion of the FS timestamp
  
  //---------- ISR spinlock ---------- 
  spinlock_t isr_lock;
};



/* ===========================================================================
Inlines
=========================================================================== */

//---------- write 8, 16, 32 ---------- 
static inline void sm500_iowrite8(int reg, uint8_t value)
{
  iowrite8(value, &((uint8_t *)sm500.BAR0)[reg]);
}

static inline void sm500_iowrite16(int reg, uint16_t value)
{
  iowrite16(value, &((uint16_t *)sm500.BAR0)[reg]);
}

static inline void sm500_iowrite32(int reg, uint32_t value)
{
  iowrite32(value, &((uint32_t *)sm500.BAR0)[reg]);
}

//---------- read 8, 16, 32 ---------- 
static inline uint8_t sm500_ioread8(int reg)
{
  return ioread8(&((uint8_t *)sm500.BAR0)[reg]);
}

static inline uint16_t sm500_ioread16(int reg)
{
  return ioread16(&((uint16_t *)sm500.BAR0)[reg]);
}

static inline uint32_t sm500_ioread32(int reg)
{
  return ioread32(&((uint32_t *)sm500.BAR0)[reg]);
}


//---------- set bits 8, 16, 32 ---------- 
static inline void sm500_set_register_bits8(int reg, uint8_t bitmask)
{
  sm500_iowrite8(sm500_ioread8(reg) | bitmask, reg);
}

static inline void sm500_set_register_bits16(int reg, uint16_t bitmask)
{
  sm500_iowrite16(sm500_ioread16(reg) | bitmask, reg);
}

static inline void sm500_set_register_bits32(int reg, uint32_t bitmask)
{
  sm500_iowrite32(sm500_ioread32(reg) | bitmask, reg);
}


//---------- clear bits 8, 16, 32 ---------- 
static inline void sm500_clear_register_bits8(int reg, uint8_t bitmask)
{
  sm500_iowrite8(sm500_ioread8(reg) & (~bitmask), reg);
}

static inline void sm500_clear_register_bits16(int reg, uint16_t bitmask)
{
  sm500_iowrite16(sm500_ioread16(reg) & (~bitmask), reg);
}

static inline void sm500_clear_register_bits32(int reg, uint32_t bitmask)
{
  sm500_iowrite32(sm500_ioread32(reg) & (~bitmask), reg);
}


#endif		//#ifndef DRVSM500_PRIVATE_H


  //----------  ---------- 
  //----------  ---------- 
  //----------  ---------- 

