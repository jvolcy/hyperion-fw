/*
 sm500.c
 sm500 driver
 Copyright (c) 2012, Micron Optics, Inc.
*/

#include <linux/autoconf.h>
#include <linux/module.h>
#include <linux/pci.h>
#include <linux/uaccess.h>

#include <linux/sched.h>
//#include <linux/wait.h>

#include "sm500_private.h"


/* ===========================================================================
sm500_ioctl()
=========================================================================== */
//----------  ----------
int sm500_ioctl(struct inode *inode, struct file *file, unsigned int cmd,
                unsigned long arg_)
{
  int err = 0;
  
//---------- union of possible argument types ----------
  union
  {
    void *data;
    struct sm500_ioctl_reg_arg  *io;
    uint8_t *pdata8;
    uint16_t *pdata16;
    uint32_t *pdata32;
    uint8_t data8;
    uint16_t data16;
    uint32_t data32;
  } arg;
  
  arg.data32 = arg_;

  switch(cmd)
  {
    case SM500_IOC_DRV_VERSION:
    	 *(arg.pdata32) = SM500_VERSION_I;
       break;

    case SM500_IOC_READ_REG8:
				arg.io->value = sm500_ioread8(arg.io->reg);
       break;
       
    case SM500_IOC_READ_REG16:
				arg.io->value = sm500_ioread16(arg.io->reg);
       break;
       
    case SM500_IOC_READ_REG32:
				arg.io->value = sm500_ioread32(arg.io->reg);
       break;

    case SM500_IOC_WRITE_REG8:
       sm500_iowrite8(arg.io->reg, (uint8_t)(arg.io->value));
       break;
       
    case SM500_IOC_WRITE_REG16:
       sm500_iowrite16(arg.io->reg, (uint16_t)(arg.io->value));
       break;
       
    case SM500_IOC_WRITE_REG32:
       sm500_iowrite32(arg.io->reg, (uint32_t)(arg.io->value));
       break;
       
    case SM500_IOC_SET_MMAP_INDEX:
       sm500.mmap_index = arg.data32;
       SM500_DBG( MSG("mmap_index set to %d\n", sm500.mmap_index); )
       break;

    case SM500_IOC_GET_PEAKS_DATA:
      if (sm500.peaks_buf_rd_ptr == sm500.peaks_buf_wr_ptr)
        {
          wait_event_interruptible(sm500.peaks_data_wq, sm500.peaks_buf_rd_ptr != sm500.peaks_buf_wr_ptr);
        }
      *(arg.pdata16) = sm500.peaks_buf_rd_ptr;
      sm500.peaks_buf_rd_ptr = (sm500.peaks_buf_rd_ptr + 1) & (sm500.NumDmaPeaksBuffers - 1);
      break;
       
    case SM500_IOC_PEAKS_DATA_READY:
      *(arg.pdata8) = sm500.peaks_data_ready;
      break;

    case SM500_IOC_GET_SPECTRUM:
//    SM500_DBG( MSG("SM500_IOC_WAIT_4_SPECTRUM\n");)

      if (sm500.fs_data_ready == 0)
      {
//        SM500_DBG( MSG("Going to sleep on fs wait queue...\n");)
        wait_event_interruptible(sm500.fs_wq, sm500.fs_data_ready);
//        SM500_DBG( MSG("Waking up...\n");)
      }
//      SM500_DBG( MSG("Returning from SM500_IOC_WAIT_4_SPECTRUM ioctl\n");)

      *(arg.pdata16) = 0;   //Assumes a single FS DMA fifo

//  Use the logic below if we ever decide to have more than 1 DAM fifo
/*
      *(arg.pdata16) = sm500.peaks_fs_rd_ptr;
      sm500.fs_buf_rd_ptr = (sm500.peaks_fs_rd_ptr + 1) & (sm500.NumFsDmaBuffers - 1);
*/
      sm500.fs_data_ready = 0;
      break;
       
    case SM500_IOC_FS_DATA_READY:
      *(arg.pdata8) = sm500.fs_data_ready;
      break;

    case SM500_IOC_CANCEL_READ:
	     sm500.fs_data_ready = 1;
	     wake_up_interruptible(&sm500.fs_wq);	//wake up any FS reader	
	          
  	   sm500.peaks_buf_rd_ptr = sm500.NumDmaPeaksBuffers; //an impossible value, readers will not block
	     wake_up_interruptible(&sm500.peaks_data_wq);	//wake up any peaks reader
       break;
       
		//---------- Default ----------
		default:
				MSG("Unknown command %u.\n", cmd);
				err = -EINVAL;
				break;
  }
  
  return err;
}









/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------
