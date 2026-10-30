@echo off
rem Builds one released version of Hoi4 Engine Patcher with the exact compiler settings used for the release.
rem Usage:  build.cmd v2.1.0   or   build.cmd v2.1.1
rem Requires: .NET SDK 9.0.302 and the .NET Framework 4.8 Developer Pack (reference assemblies).
setlocal
cd /d "%~dp0"
set "V=%~1"
if "%V%"=="" goto usage
if not exist "%V%\src\Engine.cs" goto usage
set "REF=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
set "CSC=%ProgramFiles%\dotnet\sdk\9.0.302\Roslyn\bincore\csc.dll"
if not exist "%CSC%" goto nosdk
if not exist "%REF%\mscorlib.dll" goto noref
if not exist "%V%\out" mkdir "%V%\out"
pushd "%V%"
dotnet "%CSC%" -nologo -noconfig -nostdlib+ -langversion:12 -deterministic -optimize+ -nullable:disable ^
  -target:winexe -platform:anycpu "-out:out\Hoi4 Engine Patcher.exe" ^
  "-r:%REF%\mscorlib.dll" "-r:%REF%\System.dll" "-r:%REF%\System.Core.dll" "-r:%REF%\System.Drawing.dll" "-r:%REF%\System.Windows.Forms.dll" ^
  -win32icon:app.ico -win32manifest:app.manifest "-resource:app.ico,Hoi4EnginePatcher.app.ico" ^
  src\AssemblyInfo.cs src\Engine.cs src\Forms.cs src\PatchData.g.cs src\Program.cs src\Steam.cs src\Texts.g.cs src\Theme.cs
set "RC=%ERRORLEVEL%"
popd
if not "%RC%"=="0" exit /b %RC%
certutil -hashfile "%V%\out\Hoi4 Engine Patcher.exe" SHA256
exit /b 0

:usage
echo Usage: build.cmd v2.1.0  or  build.cmd v2.1.1
exit /b 1
:nosdk
echo .NET SDK 9.0.302 not found: "%CSC%"
exit /b 1
:noref
echo .NET Framework 4.8 reference assemblies not found: "%REF%"
exit /b 1
