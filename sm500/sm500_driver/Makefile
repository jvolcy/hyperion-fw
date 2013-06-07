# Makefile for Micron Optics sm500 driver
# Copyright (c) 2012, Micron Optics
#

CONFIG_DRV_SM500=m

obj-$(CONFIG_DRV_SM500) += sm500.o
sm500-objs := sm500_driver.o
sm500-objs += sm500_ioctl.o

all:
	make -C /lib/modules/$(shell uname -r)/build M=$(CURDIR) modules

clean:
	rm -f *.o
	make -C /lib/modules/$(shell uname -r)/build M=$(CURDIR) clean



