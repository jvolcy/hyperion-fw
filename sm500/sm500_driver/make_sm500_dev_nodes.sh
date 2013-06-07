#!/bin/bash

if [ "$(whoami)" != "root" ]
then
     echo MUST BE LOGGED IN AS ROOT!
     exit 0
else
     echo Logged in as root . . . check!
fi

major=$(awk '/sm500/ {print $1}' /proc/devices)
mknod /dev/sm500 c $major 0
chmod a+w /dev/sm500

