@echo off
setlocal enabledelayedexpansion

echo ===========================================
echo   Tiwut Launcher Build System
echo ===========================================
echo.
echo Please choose an option:
echo [1] Clean the project
echo [2] Build standalone release (.exe)
echo.

set /p choice="Enter option (1 or 2): "

if "%choice%"=="1" (
    echo.
    echo Terminating running launcher processes...
    taskkill /f /im TiwutLauncher.exe /t >nul 2>&1
    dotnet build-server shutdown >nul 2>&1
    timeout /t 1 /nobreak >nul 2>&1

    echo Cleaning project...
    dotnet clean -c Debug >nul 2>&1
    dotnet clean -c Release >nul 2>&1

    call :SafeDelete bin
    call :SafeDelete obj
    call :SafeDelete dist
    call :SafeDelete .vs

    for /d /r %%i in (*.WebView2) do (
        if exist "%%i" call :SafeDelete "%%i"
    )

    echo Project cleaned successfully.
    pause
    exit /b 0
)

if "%choice%"=="2" (
    echo.
    echo Terminating running launcher processes...
    taskkill /f /im TiwutLauncher.exe /t >nul 2>&1
    timeout /t 1 /nobreak >nul 2>&1

    echo Building standalone release executable...
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false
    
    if !ERRORLEVEL! equ 0 (
        echo.
        echo Copying built files to 'dist' folder...
        call :SafeDelete dist
        mkdir dist
        xcopy /s /e /y "bin\Release\net8.0-windows\win-x64\publish\*" "dist\"
        
        echo.
        echo ===========================================
        echo BUILD SUCCESSFUL!
        echo Standalone executable created at: dist\TiwutLauncher.exe
        echo ===========================================
    ) else (
        echo.
        echo ===========================================
        echo BUILD FAILED. Please check compiler errors.
        echo ===========================================
    )
    pause
    exit /b !ERRORLEVEL!
)

echo Invalid choice.
pause
exit /b 1

:SafeDelete
if not exist "%~1" exit /b 0
for /l %%x in (1, 1, 5) do (
    if exist "%~1" (
        rmdir /s /q "%~1" >nul 2>&1
        if not exist "%~1" exit /b 0
        timeout /t 1 /nobreak >nul 2>&1
    ) else (
        exit /b 0
    )
)
exit /b 0
