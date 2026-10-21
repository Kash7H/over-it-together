@echo off
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize+ /platform:x64 /target:exe /out:"%~dp0OverItTogether.exe" "%~dp0src\OverItTogether.cs"
