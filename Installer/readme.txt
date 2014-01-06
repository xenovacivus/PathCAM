You'll need Wix to build the installer:

http://wixtoolset.org/

Make sure the <installdir>\bin directory is in the PATH 
environment variable.  That allows candle.exe and light.exe 
to be executed from scripts without providing the full path 
all the time.

Then just build the VS solution in release mode and double click run.bat