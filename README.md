# Http Memory Leak Check
## Summary
This project creates a sample android xamarin application with couchbase lite.  It will then attempt to add documents with data blob attachments and upload them to a sync gateway.  We are then checking the amount of memory being utilized by the application to determine if there is a memory leak as previously discovered when uploading and disposing of an http client.

## Memory Check
To investigate the amount of memory in use by the application the following command is used from bash.

`while sleep 1; do adb shell dumpsys meminfo | grep "HttpMemory"; done`