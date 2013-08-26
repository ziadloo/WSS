@echo off
IF NOT EXIST %2 (mkdir %2)
xcopy %1 %2 /Y
