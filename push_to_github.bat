@echo off
title Push to GitHub - FloatingMediaBar
echo ========================================================
echo   Pushing FloatingMediaBar to GitHub
echo   Repository: https://github.com/j7x69/FloatingMediaBar
echo ========================================================
echo.
echo GitHub yeu cau Personal Access Token (PAT) de push code qua HTTPS.
echo Neu ban chua co Token, tao nhanh tai:
echo https://github.com/settings/tokens/new (Chon quyen 'repo')
echo.
set /p TOKEN="Dan GitHub Personal Access Token vao day: "

if "%TOKEN%"=="" (
    echo Token khong duoc de trong!
    pause
    exit /b
)

echo.
echo Dang day code len GitHub...
"C:\Users\j7x6\MinGit\cmd\git.exe" push https://%TOKEN%@github.com/j7x69/FloatingMediaBar.git main

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   [THANH CONG] Code da duoc upload len GitHub!
    echo   Xem tai: https://github.com/j7x69/FloatingMediaBar
    echo ========================================================
) else (
    echo.
    echo [THAT BAI] Vui long kiem tra lai Token hoac quyen truy cap.
)
echo.
pause
