/* ===========================================================================
 sm500_driver.c
 sm500 driver

 Jerry Volcy

 Copyright (c) 2013, Micron Optics, Inc.
=========================================================================== */

#include <linux/module.h>
#include <linux/kernel.h>
#include <linux/init.h>
#include <linux/autoconf.h>
#include <linux/module.h>
#include <linux/sched.h>
#include <linux/pci.h>
#include <linux/fs.h>
#include <linux/errno.h>
#include <linux/time.h>
#include <linux/interrupt.h>
#include <linux/dma-mapping.h>
#include <linux/semaphore.h>
#include <linux/uaccess.h>
#include <asm/dma.h>

#include "sm500_private.h"

/* ===========================================================================
Misc. Constants
=========================================================================== */
#define PCI_VENDOR_ID_MOI    0x1c1c
#define PCI_DEVICE_ID_SM500    0x0500


/* ===========================================================================
Macros
=========================================================================== */


/* ===========================================================================
Globals
=========================================================================== */
int sm500_major = SM500_MAJOR;  //character driver major #
int sm500_minor = SM500_MINOR;  //character driver minor #
module_param(sm500_major, int, 0);
module_param(sm500_minor, int, 0);

struct dev_sm500 sm500;    //sm500 device context

/* ===========================================================================
PCI Table Entry
=========================================================================== */
static DEFINE_PCI_DEVICE_TABLE(sm500_pci_tbl) =
{
  { PCI_VDEVICE(MOI, PCI_DEVICE_ID_SM500)},
  { 0 }        /* 0 terminated list. */
};
MODULE_DEVICE_TABLE(pci, sm500_pci_tbl);

/* ===========================================================================
Forward Declarations
=========================================================================== */




/* ===========================================================================
sm500_dsiable_interrupts()
=========================================================================== */
static void inline sm500_disable_interrupts(void)
{
  sm500_iowrite32(SM500_REG_INTE, 0);
  
  /* after we disable interrupts, we need to wake up any readers
  that may be sleeping on a wait queue.  Otherwise, these readers
  remain asleep indefinitely. */
  if (sm500.bWaitQueueInitialized)
  {
    wake_up_interruptible(&sm500.peaks_data_wq);
    wake_up_interruptible(&sm500.fs_wq);
  }
}


/* ===========================================================================
sm500_dsiable_DMA()
=========================================================================== */
static void inline sm500_disable_DMA(void)
{
  sm500_iowrite32(SM500_REG_DMACR, 0);
}

/* ===========================================================================
sm500_free_dma_buffers()
=========================================================================== */
static void sm500_free_dma_buffers(void)
{
  int i;

  //disable all DMAs before freeing DMA buffers
  sm500_disable_DMA();

  SM500_DBG(MSG("Attempting to free all allocated DMA buffers:");)
  //free any allocated peaks DMA buffers
  for (i=0; i<sm500.NumDmaPeaksBuffers; i++)
  {
    if (sm500.dma_peaks_buffer[i].kernel_addr != 0)
      {
        pci_free_consistent(sm500.dev, sm500.DmaPeaksBufferSize, 
        	sm500.dma_peaks_buffer[i].kernel_addr, sm500.dma_peaks_buffer[i].bus_addr);
        SM500_DBG(MSG("Freed %d bytes from peaks DMA buffer #%d.\n", sm500.DmaPeaksBufferSize, i);)
      }    
    sm500.dma_peaks_buffer[i].kernel_addr = 0;  //zero the kern address to indicate this buffer is un-allocated
  }
    
  //free any allocated FS DMA buffers  
  for (i=0; i<sm500.NumDmaFsBuffers; i++)
  {
    if (sm500.dma_fs_buffer[i].kernel_addr != 0)
    {
      pci_free_consistent(sm500.dev, sm500.DmaFsBufferSize,
      	sm500.dma_fs_buffer[i].kernel_addr, sm500.dma_fs_buffer[i].bus_addr);
      SM500_DBG(MSG("Freed %d bytes from fs DMA buffer #%d.\n", sm500.DmaFsBufferSize, i);)
    }
    sm500.dma_fs_buffer[i].kernel_addr = 0;    //zero the kern address to indicate this buffer is un-allocated
  }

  //free the allocated dma_buffer pointers
  kfree(sm500.dma_peaks_buffer);
  kfree(sm500.dma_fs_buffer);      
}


/* ===========================================================================
sm500_open()
=========================================================================== */
static int sm500_open(struct inode *inode, struct file *file)
{
  if (sm500.open_count == SM500_MAX_NUM_CLIENTS)
    return -EAGAIN;

  //Set the DMA read pointer to the current pointer position
  sm500.peaks_buf_rd_ptr = sm500.peaks_buf_wr_ptr;
  sm500.fs_buf_rd_ptr = sm500.fs_buf_wr_ptr;
  sm500.open_count++;
  return 0;
}

/* ===========================================================================
sm500_close()
=========================================================================== */
static int sm500_close(struct inode *inode, struct file *file)
{
/* Need to verify whetehr or not this gets called when open() fails */

  sm500.open_count--;
  return 0;
}


/* ===========================================================================
sm500_mmap()

Set the mmap_index with the SM500_IOC_SET_MMAP_INDEX ioctl.

From user-space: 
 - FS buffer memory should be specified as (SM500_MMAP_FS_BUFFER + N),
   where N is the FS buffer index.
 - Peaks buffer memory should be specified as (SM500_MMAP_PEAKS_BUFFER + N),
   where N is the peaks buffer index.
=========================================================================== */
static int sm500_mmap(struct file *filp, struct vm_area_struct *vma)
{
  int err = 0;
  int index = sm500.mmap_index & 0xFFFF;  //mask out FS vs Peaks selection bits

  if (sm500.mmap_index & SM500_MMAP_FS_BUFFER)  //mapping FS buffers
  {
    if ( index >= sm500.NumDmaFsBuffers )
    {
      SM500_DBG(MSG("mmap(): Illegal FS buffer index %d\n", index);)
      return -EAGAIN;    //illegal buffer index
    }
       
    err = remap_pfn_range(vma, vma->vm_start,
    									sm500.dma_fs_buffer[index].bus_addr >> PAGE_SHIFT,
                      vma->vm_end - vma->vm_start,
                      vma->vm_page_prot);
  }
  else    //mapping Peaks buffers
  {
    if ( index >= sm500.NumDmaPeaksBuffers )
    {
      SM500_DBG(MSG ("mmap(): Illegal peaks buffer index %d\n", index);)
      return -EAGAIN;    //illegal buffer index
    }
      
    err = remap_pfn_range(vma, vma->vm_start,
    									sm500.dma_peaks_buffer[index].bus_addr >> PAGE_SHIFT,
                      vma->vm_end - vma->vm_start,
                      vma->vm_page_prot);                      
  }

  sm500.mmap_index++;    //auto-increment to next buffer
  
  if (err)
  {
    MSG("mmap() failed in mapping buffer # %d\n", index); 
  }

  return err;
}


/* ===========================================================================
sm500_ISR() 
Interrupt Service Routine for sm500
=========================================================================== */
static irqreturn_t sm500_ISR(int irq, void *data)
{
  uint32_t int_flag;
  struct timespec current_time;

	//---------- Read and clear the interrupt flag ----------
  /* By immediately clearing the flag upon reading it, we minimize the chances
  that the flag will be set while we are in the ISR.  If that were to happen,
  than clearing the flag at the end of the ISR would clear the flag and cause
  us to miss the processing of an interrupt. */
  int_flag = sm500_ioread32(SM500_REG_INTF);    //read the interrupt flag
	sm500_iowrite32(SM500_REG_INTF, SM500_INT_CLEAR);	//immediately clear the interrupt flags

	//---------- No flag set ----------
  /* This potentially happens when multiple peaks data sets were processed on the
  last interrupt.*/
  if (int_flag == 0)
  {
    MSG("Interrupt Flag is zero... nothing to do.\n");
    return IRQ_HANDLED;
  }
  
  /* The use of the spinlock here is somewhat unitelligent.  We basically encase the entire ISR
  in one huge lock rather than targeting specific registers (like buf_wr_ptr).  Because of the
  strategy of reading the int_flag and immediately clearing the SM500_REG_INTF register, there
  isn't much opportunity to miss any interrupts.  One the other hand, if we do get concurrent
  interrupts, the spinlock prevents the already unlikely scenario where we err in adjusting the
  buf_wr_ptr.  A more intelligent use of the spinlock might be to protect this single register.
  The current implementation is safe and is not predicted to cause any performance degredation
  as concurrent interrupts are not expected. */
  spin_lock(&sm500.isr_lock);

	//---------- Full Spectrum Interrupt ----------
  if (int_flag & SM500_INT_FS)
  {
    /* This block of logic will need to be re-worked if we ever decide to have more than 1 FS
    buffer.  For now, the logic can't be included because the serial number scheme of keeping
    track of buffer indeces is not in place for FS buffers. */

    //timestamp the FS with the previously stored S/N
    ((uint32_t*)sm500.dma_fs_buffer[sm500.fs_buf_wr_ptr].kernel_addr)[sm500.DmaBufferSnOffset32] = sm500.fs_timestamp_sec;
    ((uint32_t*)sm500.dma_fs_buffer[sm500.fs_buf_wr_ptr].kernel_addr)[sm500.DmaBufferSnOffset32+1] = sm500.fs_timestamp_nsec;

    //increment the FS pointer
//    sm500.target_fs_buf_index = (sm500.target_fs_buf_index 1) & (sm500.NumDmaFsBuffers-1);

    //wake up FS readers
		sm500.fs_data_ready = 1;
		wake_up_interruptible(&sm500.fs_wq);	//wake up any FS reader
  }

	//---------- Peaks Interrupt ----------
  if (int_flag & SM500_INT_PK)
  {
    getnstimeofday(&current_time);

		/* Set the peak buffer index to the next location for writing.  Pointing to the
	  next location for writing (the oldest data set) rather than pointing to the
	  newest data set makes the logic of comparing the read and write pointers
  	easier. When the 2 pointers are equal, we put readers on a wait queue.
	  If the target index pointed to the newest data set, then we would have no
  	way of knowing whether or not a reader has already read the newest data
	  set. */
    sm500.target_pk_buf_index = (sm500_ioread32(SM500_REG_DMASNLO) + 1) & (sm500.NumDmaPeaksBuffers-1);
	  /* The above logic fails when the SerialNumber rolls and NumDmaPeaksBuffers is
  	not a power of 2.  An alternate implementation of 
  	target_index = (sm500.SerialNumber + 1) % sm500.NumDmaPeaksBuffers;
  	is computationally more expensive and suffers the same failure under the
  	same conditions.  While it is possible to properly handle this, the computational
  	cost is non-ideal for an ISR.  Instead, for this ISR, we will require that
  	the NumDmaPeaksBuffers and NumDamFsBuffers be a power of 2. */


    while (sm500.peaks_buf_wr_ptr != sm500.target_pk_buf_index)
    {

      //---------- timestamp the data set ----------
      ((uint32_t*)sm500.dma_peaks_buffer[sm500.peaks_buf_wr_ptr].kernel_addr)[sm500.DmaBufferSnOffset32] = (uint32_t)current_time.tv_sec;
      ((uint32_t*)sm500.dma_peaks_buffer[sm500.peaks_buf_wr_ptr].kernel_addr)[sm500.DmaBufferSnOffset32+1] = (uint32_t)current_time.tv_nsec;


//((uint32_t*)sm500.dma_peaks_buffer[sm500.peaks_buf_wr_ptr].kernel_addr)[0] = sm500_ioread32(SM500_REG_DMASNLO); //FOR TESTING ONLY;

	    //---------- check the FS bit, store the timestamp if necessary ----------
      if (int_flag & SM500_INT_FS_SET)
      {
        sm500.fs_timestamp_sec = (uint32_t)current_time.tv_sec;
        sm500.fs_timestamp_nsec = (uint32_t)current_time.tv_nsec;
      }

      sm500.peaks_buf_wr_ptr = (sm500.peaks_buf_wr_ptr + 1) & (sm500.NumDmaPeaksBuffers-1);
    }

		wake_up_interruptible(&sm500.peaks_data_wq);	//wake up any peaks reader
  
	}

  spin_unlock(&sm500.isr_lock);
  
  return IRQ_HANDLED;
}


/* ===========================================================================
sm500_probe()
called when a matching device is found, once for each 
=========================================================================== */
static int  __devinit sm500_probe(struct pci_dev *pdev, const struct pci_device_id *id)
{
  int i;
  int err;

  SM500_DBG
  (
    uint16_t vid;
    uint16_t did;
  )

  union {
    uint32_t  ifirmware;
    char cfirmware[4];
  } uversion;

  sm500.dev = pdev;  //store the pci dev pointer in our device structure


//---------- Enable the PCI device ----------
  if ( (err = pci_enable_device(sm500.dev)) )
  {
    MSG("pci_enable_device() failed with error code 0x%x.\n", err);
    return err;
  }

//---------- mark the I/O region associated with this pci dev as being owned by this driver ----------
  if ( (err = pci_request_regions(sm500.dev, SM500_NAME)) )
  {
    MSG("pci_request_regions() failed with error code 0x%x.  Aborting...\n", err);
    goto pci_request_region_failed;
  }

//---------- request ownership of the memory region associated with this pci dev ----------
  sm500.BAR0 = pci_iomap(sm500.dev, 0, pci_resource_len(sm500.dev, 0));
  if (!sm500.BAR0) 
  {
    MSG("failed to register memory region (pci_io_map()).\n");
    err = -ENOMEM;
    goto pci_iomap_failed;
  }

//---------- debug info ----------
  SM500_DBG(
            pci_read_config_word(sm500.dev, PCI_VENDOR_ID, &vid);
            pci_read_config_word(sm500.dev, PCI_DEVICE_ID, &did);
            MSG("(%s) probing %04x:%04x\n", pci_name(sm500.dev), vid, did);
            MSG("resource start: %08lx, end:%08lx, \
              flags: %08lx\n",  (unsigned long)pci_resource_start(sm500.dev, 0),
              (unsigned long)pci_resource_end(sm500.dev, 0),
              pci_resource_flags(sm500.dev, 0));
            )

  uversion.ifirmware = sm500_ioread32(SM500_REG_HVER);
  MSG("firmware version %c%c%c%c\n", uversion.cfirmware[3],
                                      uversion.cfirmware[2],
                                      uversion.cfirmware[1],
                                      uversion.cfirmware[0]);

//---------- Interrupts ----------
  sm500_disable_interrupts();

  if ( (err = pci_enable_msi(sm500.dev)) )
  {
    MSG("pci_enable_msi() returned error code 0x%x\n", err);
    goto pci_enable_msi_failed;
  }

  SM500_DBG( MSG("using msi, interrupt = %d\n", sm500.dev->irq);)

  if ( (err = request_irq(sm500.dev->irq, sm500_ISR ,IRQF_SHARED, SM500_NAME, sm500.dev)) )
  {
    MSG("request_irq() failed with error code 0x%x\n", err);
    goto pci_request_irq_failed;
  }

//---------- needed ??? ----------
  pci_set_master(sm500.dev);

//---------- Allocate Peak DMA buffers ----------

  sm500.DmaBufferSnOffset32 = sm500_ioread32(SM500_REG_TSOFST) >> 2;    //32-bit offset for kernel S/N in DMA headers
  sm500.NumDmaPeaksBuffers = sm500_ioread32(SM500_REG_NPKBUF);    //the # of DMA peaks buffers
  sm500.NumDmaFsBuffers = sm500_ioread32(SM500_REG_NFSBUF);       //the # of FS buffers
  sm500.DmaPeaksBufferSize = sm500_ioread32(SM500_REG_PKBUFSZ);   //the size of an individual peaks DMA buffer
  sm500.DmaFsBufferSize = sm500_ioread32(SM500_REG_FSBUFSZ);      //the size of an individual FS DMA buffer

  SM500_DBG(MSG("DmaBufferSnOffset32 = %d.\n", sm500.DmaBufferSnOffset32);)
  SM500_DBG(MSG("NumDmaPeaksBuffers = %d.\n", sm500.NumDmaPeaksBuffers);)
  SM500_DBG(MSG("NumDmaFsBuffers = %d.\n", sm500.NumDmaFsBuffers);)
  SM500_DBG(MSG("DmaPeaksBufferSize = %d.\n", sm500.DmaPeaksBufferSize);)
  SM500_DBG(MSG("DmaFsBufferSize = %d.\n", sm500.DmaFsBufferSize);)


  //Allocate the dma_buffer structures
  sm500.dma_peaks_buffer = (struct dma_buffer*)kmalloc(sizeof(struct dma_buffer) * sm500.NumDmaPeaksBuffers, GFP_KERNEL);
  sm500.dma_fs_buffer = (struct dma_buffer*)kmalloc(sizeof(struct dma_buffer) * sm500.NumDmaFsBuffers, GFP_KERNEL);


  for (i=0; i<sm500.NumDmaPeaksBuffers; i++)
  {
    sm500.dma_peaks_buffer[i].kernel_addr = (void*)pci_alloc_consistent(sm500.dev, sm500.DmaPeaksBufferSize, &(sm500.dma_peaks_buffer[i].bus_addr));
    if (!sm500.dma_peaks_buffer[i].kernel_addr)
    {
      MSG("Failed to allocate sm500 peaks DMA buffer #%d.\n", i);
      goto pci_dma_alloc_failed;
    }

    SM500_DBG(MSG("Allocated %d bytes for peaks DMA buffer #%d.\n", sm500.DmaPeaksBufferSize, i);)

    SM500_DBG(MSG("Allocated %d bytes for peaks DMA buffer #%d  Kern Addr = 0x%08lx   Bus Addr = 0x%08lx.\n",
     sm500.DmaPeaksBufferSize, i, (unsigned long)sm500.dma_peaks_buffer[i].kernel_addr, (unsigned long)sm500.dma_peaks_buffer[i].bus_addr);)
    
    //Set FPGA peaks DMA address register
    sm500_iowrite32(SM500_REG_DMATAR0 + i, sm500.dma_peaks_buffer[i].bus_addr);
  }

//---------- Allocate FS DMA buffers ----------
  for (i=0; i<sm500.NumDmaFsBuffers; i++)
  {
    sm500.dma_fs_buffer[i].kernel_addr = (void*)pci_alloc_consistent(sm500.dev, sm500.DmaFsBufferSize, &(sm500.dma_fs_buffer[i].bus_addr));
    if (!sm500.dma_fs_buffer[i].kernel_addr)
    {
      MSG("Failed to allocate sm500 FS DMA buffer #%d.\n", i);
      goto pci_dma_alloc_failed;
    }
    
    SM500_DBG(MSG("Allocated %d bytes for fs DMA buffer #%d  Kern Addr = 0x%08lx   Bus Addr = 0x%08lx.\n",
     sm500.DmaFsBufferSize, i, (unsigned long)sm500.dma_fs_buffer[i].kernel_addr, (unsigned long)sm500.dma_fs_buffer[i].bus_addr);)

    //Set FPGA fs DMA address register
    sm500_iowrite32(SM500_REG_DMAFSAR + i, sm500.dma_fs_buffer[i].bus_addr);
    
  }


//---------- success ----------
  SM500_DBG(MSG("probe ok\n");)
  return 0;


pci_dma_alloc_failed:
  //---------- free DMA buffers ----------
  MSG("Freeing all previously allocated buffers.\n");
  sm500_free_dma_buffers();
  err = -EFAULT;    //EFAULT??
  free_irq(sm500.dev->irq, sm500.dev);
pci_request_irq_failed:
  pci_disable_msi(sm500.dev);
pci_enable_msi_failed:
  iounmap(sm500.BAR0);
  sm500.BAR0 = NULL;
pci_iomap_failed:
  pci_release_regions(sm500.dev);
pci_request_region_failed:
  pci_disable_device(sm500.dev);

  return err;
}



/* ===========================================================================
sm500_remove() 
called once for each successfully probed device 
=========================================================================== */
static void __devexit sm500_remove(struct pci_dev *pdev)
{
  SM500_DBG(MSG("remove\n");)
  sm500_disable_interrupts();
  sm500_disable_DMA();
  free_irq(sm500.dev->irq, sm500.dev);
  pci_disable_msi(sm500.dev);
  pci_clear_master(sm500.dev);    //needed ??
  pci_release_regions(sm500.dev);
  sm500_free_dma_buffers();  
  iounmap(sm500.BAR0);
  pci_disable_device(sm500.dev);
  return;
}

/* ===========================================================================
sm500 PCI driver
=========================================================================== */
struct pci_driver sm500_driver = {
  .name           = SM500_NAME,
  .id_table       = sm500_pci_tbl,
  .probe          = sm500_probe,
  .remove         = __devexit_p(sm500_remove),
};

/* ===========================================================================
sm500 File Operations
=========================================================================== */
struct file_operations sm500_fops = {
  owner:    THIS_MODULE,
  open:     sm500_open,
  release:  sm500_close,
  ioctl:    sm500_ioctl,
  mmap:     sm500_mmap,
};


/* ===========================================================================
sm500_init()
=========================================================================== */
static int __init sm500_init(void)
{
  int i, err;
  dev_t devt;
  
//----------  zero out dma buffer addressess ----------
/* we zero out the buffers to indicate that they are not currently allocated.
If this init function fails after the buffers are successfully allocated,
then we can de-allocate as needed.  We zero the buffers here before any
function that can cause an init failure is called. */
  for (i = 0; i < sm500.NumDmaPeaksBuffers; i++)
  {
    sm500.dma_peaks_buffer[i].kernel_addr = 0;
    sm500.dma_peaks_buffer[i].bus_addr = 0;
  }

  for (i = 0; i < sm500.NumDmaFsBuffers; i++)
  {
    sm500.dma_fs_buffer[i].kernel_addr = 0;
    sm500.dma_fs_buffer[i].bus_addr = 0;
  }
  
  sm500.fs_data_ready = 0;  //Set to zero = no fs data ready for user app
  sm500.peaks_data_ready = 0;  //Set to zero = no peaks data ready for user app


//----------  initialize the buffer pointers ----------
  sm500.peaks_buf_wr_ptr = 0;      //peaks buffer write pointer (the next location for writing = points to the oldest data set)
  sm500.peaks_buf_rd_ptr = 0;      //peaks buffer read pointer (the next location for reading = points to the newst data set)
  sm500.fs_buf_wr_ptr = 0;         //fs buffer write pointer
  sm500.fs_buf_rd_ptr = 0;         //fs buffer read pointer

  
//----------  invalidate the wait queues ----------
  sm500.bWaitQueueInitialized = 0;    //indicate that the WQs have not yet been initialized
  

  MSG("====================================================\n");
  MSG("Micron Optics SM500 driver v %d.%d\n", SM500_VERSION_I>>16, SM500_VERSION_I & 0xFFFF);
  if (sm500_major != 0)  //no auto-assignment of major number; use explicitly specified values
  {
    devt = MKDEV(sm500_major, sm500_minor);
    err = register_chrdev_region(devt, SM500_MAXCARDS, SM500_NAME);
  }
  else  //major # set to zero ==> let the system auto-assign a major number 
  {
    err = alloc_chrdev_region(&devt, sm500_minor, SM500_MAXCARDS, SM500_NAME);
    sm500_major = MAJOR(devt);  //read back the system assigned major # here.
    sm500_minor = MINOR(devt);
  }

  if (err < 0)
  {
    MSG("Failed to register character region\n");
    return err;
  }

  SM500_DBG(MSG("Major # = %d,  First Minor # = %d, count = %d\n", sm500_major, sm500_minor, SM500_MAXCARDS);)

  cdev_init(&sm500.sm500_cdev, &sm500_fops);  //initialize char device and specify allowed file operations
  
  if (cdev_add(&sm500.sm500_cdev, devt, SM500_MAXCARDS))  //add to the kernel char device list
  {
    goto cdev_add_failed;
  }

  sm500.dev = NULL;  //set the PCI device pointer to NULL.

  if (pci_register_driver(&sm500_driver))  //register the driver
  {
    goto pci_register_driver_failed;
  }


//---------- Initialize wait queues ----------
  SM500_DBG(MSG("Initializing wait queues...\n");)
  init_waitqueue_head(&sm500.peaks_data_wq);
  init_waitqueue_head(&sm500.fs_wq);
  sm500.bWaitQueueInitialized = 1;    //indicate that the WQs have been initialized

//---------- Initialize ISR spinlock ----------
  spin_lock_init(&sm500.isr_lock);
  
  SM500_DBG(MSG("sm500_init() ok.");)
  return 0;

pci_register_driver_failed:  //delete cdev
  cdev_del(&sm500.sm500_cdev);

cdev_add_failed:  //unregister the character device region.
  unregister_chrdev_region(devt, SM500_MAXCARDS);
  return err;
}

/* ===========================================================================
sm500_exit()
=========================================================================== */
static void __exit sm500_exit(void)
{
  SM500_DBG(MSG("exiting\n");)

  pci_unregister_driver(&sm500_driver);
  cdev_del(&sm500.sm500_cdev);
  unregister_chrdev_region(MKDEV(sm500_major, sm500_minor), SM500_MAXCARDS);
  return;
}


/* ===========================================================================
Misc. module management
=========================================================================== */
module_init(sm500_init);
module_exit(sm500_exit);

MODULE_AUTHOR("Jerry Volcy");
MODULE_DESCRIPTION("MOI SM500 PCIe Driver");
MODULE_LICENSE("GPL");


/* ===========================================================================
=========================================================================== */
//----------  ----------
//----------  ----------

