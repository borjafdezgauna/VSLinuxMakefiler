# VSLinuxMakefiler

I usually program C++ with Visual Studio. VS allows you to remotely build your code on a linux machine using VSLinux, but VSLinux projects do not work in typical Continuous Integration platforms (Azure, AppVeyor, Travis, ...).

The main purpose of this project is to use Continuous Integration on a Linux machine (Azure, AppVeyor...)
This application generates automatically a linux script for Visual Studio solutions (working on VS2017 Community) that:

- Builds all the [VSLinux projects](https://devblogs.microsoft.com/cppblog/linux-development-with-c-in-visual-studio/) with support for:
  - Project references (they are ordered before building)
  - Library dependencies
- Builds the C++ unit tests (only MSTest projects) as linux executables
- Runs all the C++ unit tests and prints the results on the standard output


