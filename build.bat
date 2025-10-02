@echo off
REM Occop 快速构建脚本
REM 用途：一键构建和发布Occop应用程序

SETLOCAL EnableDelayedExpansion

echo ========================================
echo       Occop 构建和发布工具
echo ========================================
echo.

REM 检查dotnet是否安装
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [错误] 未找到dotnet命令
    echo 请先安装 .NET 6.0 SDK
    echo 下载地址: https://dotnet.microsoft.com/download/dotnet/6.0
    pause
    exit /b 1
)

echo [信息] 检测到 .NET SDK:
dotnet --version
echo.

REM 设置变量
set SOLUTION=src\Occop.sln
set PROJECT=src\Occop.UI\Occop.UI.csproj
set OUTPUT=publish\win-x64
set CONFIG=Release

echo ========================================
echo 构建配置:
echo   解决方案: %SOLUTION%
echo   主项目:   %PROJECT%
echo   输出目录: %OUTPUT%
echo   构建配置: %CONFIG%
echo ========================================
echo.

pause

REM 步骤1: 清理
echo.
echo [1/5] 清理旧构建文件...
echo ----------------------------------------
dotnet clean %SOLUTION% --configuration %CONFIG%
if exist %OUTPUT% (
    echo 删除旧的发布文件...
    rmdir /s /q %OUTPUT%
)
echo [完成] 清理完成
echo.

REM 步骤2: 还原依赖
echo [2/5] 还原NuGet包...
echo ----------------------------------------
dotnet restore %SOLUTION%
if %ERRORLEVEL% NEQ 0 (
    echo [错误] NuGet包还原失败
    pause
    exit /b 1
)
echo [完成] 依赖还原完成
echo.

REM 步骤3: 构建
echo [3/5] 构建项目...
echo ----------------------------------------
dotnet build %SOLUTION% --configuration %CONFIG% --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo [错误] 项目构建失败
    pause
    exit /b 1
)
echo [完成] 构建成功
echo.

REM 步骤4: 运行测试（可选）
echo [4/5] 运行测试（可选，按任意键跳过）...
echo ----------------------------------------
choice /C YN /M "是否运行测试"
if !ERRORLEVEL! EQU 1 (
    dotnet test %SOLUTION% --configuration %CONFIG% --no-build --verbosity normal
    if !ERRORLEVEL! NEQ 0 (
        echo [警告] 部分测试失败
        choice /C YN /M "是否继续发布"
        if !ERRORLEVEL! EQU 2 (
            echo 已取消发布
            pause
            exit /b 1
        )
    ) else (
        echo [完成] 所有测试通过
    )
) else (
    echo [跳过] 测试已跳过
)
echo.

REM 步骤5: 发布
echo [5/5] 发布应用程序...
echo ----------------------------------------
echo 正在生成自包含单文件可执行文件...
echo 这可能需要几分钟时间...
echo.

dotnet publish %PROJECT% ^
  --configuration %CONFIG% ^
  --runtime win-x64 ^
  --self-contained true ^
  --output %OUTPUT% ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:PublishReadyToRun=true ^
  --no-restore

if %ERRORLEVEL% NEQ 0 (
    echo [错误] 发布失败
    pause
    exit /b 1
)
echo [完成] 发布成功
echo.

REM 显示结果
echo ========================================
echo           构建完成！
echo ========================================
echo.
echo 可执行文件位置:
echo   %OUTPUT%\Occop.UI.exe
echo.

REM 计算文件大小
for %%A in (%OUTPUT%\Occop.UI.exe) do (
    set size=%%~zA
    set /a sizeMB=!size! / 1048576
    echo 文件大小: !sizeMB! MB
)
echo.

REM 提示后续步骤
echo 后续步骤:
echo   1. 测试可执行文件: %OUTPUT%\Occop.UI.exe
echo   2. 查看日志配置: src\Occop.UI\nlog.config
echo   3. 创建安装程序（可选）
echo.

REM 询问是否运行
choice /C YN /M "是否立即运行应用程序"
if !ERRORLEVEL! EQU 1 (
    start "" "%OUTPUT%\Occop.UI.exe"
)

echo.
echo 按任意键退出...
pause >nul

ENDLOCAL
