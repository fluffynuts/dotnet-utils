@echo off
net session >nul 2>&1
if "%ERRORLEVEL%" == "0" (
    goto start:
)

echo This script must be run as Administrator
exit /b 666

:start
setlocal EnableDelayedExpansion
set MODE=unknown
set LOGFOLDER=%2
set INTERACTIVE=1
set DEFAULT_LOG_FOLDER=C:\fusion-logs

if "%1" == "--enable" (
    set MODE=enable
    set INTERACTIVE=0
) else if "%1" == "--disable" (
    set MODE=disable
    set INTERACTIVE=0
) else if "%1" == "--help" (
    call :show_help
    exit /b 0
) else if not "%1" == ""  (
    call :show_help
    exit /b 1
)
if "%INTERACTIVE%" == "1" (
    choice /c:ed /N /M "Would you like to (e)nable or (d)isable Fusion logging? "
    if "!ERRORLEVEL!" == "0" echo "WHAT THE ACTUAL"
    if "!ERRORLEVEL!" == "1" set MODE=enable
    if "!ERRORLEVEL!" == "2" set MODE=disable

    if "!MODE!" == "enable" (
        echo WARNING: enabling Fusion logs has a serious performance impact
        choice /M "Are you sure you would like to continue?"
        if "!ERRORLEVEL!" == "2" (
            exit /b 13
        )
    )
)

if "%MODE%" == "enable" (
    if "!INTERACTIVE!" == "0" (
        set LOGFOLDER=!DEFAULT_LOG_FOLDER!
    )
    if "!LOGFOLDER!" == "" (
        set /p SELECTED_LOG_FOLDER="Where should logs be kept? Enter a full path or just press enter to accept the default (!DEFAULT_LOG_FOLDER!) "
        if "!SELECTED_LOG_FOLDER!" == "" (
            set LOGFOLDER=!DEFAULT_LOG_FOLDER!
        ) else (
            set LOGFOLDER=%SELECTED_LOG_FOLDER%
        )
    )

    if not exist "!LOGFOLDER!\" (
        mkdir "!LOGFOLDER!"
    )
    if not exist "!LOGFOLDER!\" (
        echo Unable to ensure log folder !LOGFOLDER! exists; bailing out
        exit /b 7
    )
)

set REGKEY=HKLM\SOFTWARE\Microsoft\Fusion
set ADD=reg add %REGKEY% /v
set DEL=reg delete %REGKEY% /v
set QRY=reg query %REGKEY% /v
if "%MODE%" == "enable" (
    call :enable_flag EnableLog
    call :enable_flag ForceLog
    call :enable_flag LogFailures
    call :enable_flag LogResourceBinds
    call :set_log_folder !LOGFOLDER!
) else if "%MODE%" == "disable" (
    call :delete_key EnableLog
    call :delete_key ForceLog
    call :delete_key LogFailures
    call :delete_key LogResourceBinds
    call :delete_log_folder
) else (
    echo "How did you get here?"
    exit /b 42
)

call :reset_iis
echo.
echo WARNING: You MUST restart any other relevant processes for this to take effect

exit /b 0

:reset_iis
if "!INTERACTIVE!" == "1" (
    choice /M "Restart IIS now?"
    if "!ERRORLEVEL!" == "2" (
        exit /b 0
    )
) else (
    echo Restarting IIS...
)
iisreset
exit /b 0

:set_log_folder
reg add %REGKEY% /v LogPath /d %~1 /f >nul 2>&1
if "!ERRORLEVEL!" == "0" exit /b 0
echo Could not set %REGKEY% LogPath
exit /b 5

:delete_log_folder
reg query %REGKEY% /v LogPath >nul 2>&1
if "!ERRORLEVEL!" == "1" (
    rem key does not exist, and that's ok
    exit /b 0
)
reg delete %REGKEY% /v LogPath /f >nul 2>&1
exit /b 0

:enable_flag
reg add %REGKEY% /v %~1 /t REG_DWORD /d 1 /f >nul 2>&1
if "!ERRORLEVEL!" == "0" exit /b 0
echo "Could not set flag %REGKEY% / %~1"
exit /b 5

:delete_key
reg query %REGKEY% /v %~1 >nul 2>&1
if "!ERRORLEVEL!" == "1" (
    rem key does not exist, and that's ok
    exit /b 0
)
if "!ERRORLEVEL!" == "0" reg delete %REGKEY% /v %~1 /f >nul 2>&1
if "!ERRORLEVEL!" == "0" exit /b 0
echo "Cannot delete flag %REGKEY% / %~1"
exit /b 5

:show_help
setlocal
echo usage: %~f0 [--enable|--disable] [path to log folder]
echo  --enable     Enables Fusion logging
echo  --disable    Disables Fusion logging
echo  interactive mode enabled when neither --enable or --disable are specified
echo  default log folder is !DEFAULT_LOG_FOLDER!
endlocal
