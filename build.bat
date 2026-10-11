@echo off
echo ========================================================
echo   Compiling Floating Media Studio (WPF + WinRT)
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set WPF_LIB="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
set SYS_RUNTIME="C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll"
set WINMD="C:\Windows\System32\WinMetadata"

%CSC% /target:winexe /optimize+ /lib:%WPF_LIB% /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:%SYS_RUNTIME% /r:%WINMD%\Windows.Foundation.winmd /r:%WINMD%\Windows.Media.winmd /r:%WINMD%\Windows.Storage.winmd /out:FloatingMediaBar.exe FloatingMediaBar.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] FloatingMediaBar.exe compiled successfully!
    echo.
) else (
    echo.
    echo [FAILED] Compilation failed with error code %ERRORLEVEL%.
    echo.
)
pause
