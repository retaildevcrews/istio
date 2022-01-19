@echo off

start "4120" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -m --port 4120
start "4121" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4121 -s http://localhost:4120
start "4122" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4122 -s http://localhost:4121
start "4123" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4123 -s http://localhost:4122
start "4124" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4124 -s http://localhost:4123
start "4125" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4125 -s http://localhost:4124


rem start "4126" /HIGH /MIN bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4126 -s http://localhost:4125
rem start bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4127 -s http://localhost:4126
rem start bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4128 -s http://localhost:4127
rem start bin\debug\netcoreapp3.1\aspnetapp -a webapi --port 4129 -s http://localhost:4128


