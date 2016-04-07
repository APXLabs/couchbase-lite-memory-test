# Http Memory Leak Check
## Summary
This project creates a sample android xamarin application with couchbase lite.  It will then attempt to add documents with data blob attachments and upload them to a sync gateway.  We are then checking the amount of memory being utilized by the application to determine if there is a memory leak as previously discovered when uploading and disposing of an http client.

## Configuring
Before running change the Host field to the ip address of a sync gateway configured with a username and password.
The default test is with a 50MB data blob.  This tends to crash the application within 3 blobs.  Changing this to 19MBs tends to much longer and can get up to 200 blobs pushed.

## Memory Check
To investigate the amount of memory in use by the application the following command is used from bash.

`while sleep 1; do adb shell dumpsys meminfo | grep "HttpMemory"; done`