@echo off
REM Occop 测试运行脚本 (Windows)
REM 用于本地开发和CI环境中运行测试

setlocal enabledelayedexpansion

REM 默认配置
set TEST_TYPES=All
set PARALLEL=true
set COVERAGE=true
set VERBOSITY=normal
set FAIL_FAST=false
set OUTPUT_DIR=TestResults
set ENVIRONMENT=

REM 解析命令行参数
:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="-t" set TEST_TYPES=%~2& shift& shift& goto parse_args
if /i "%~1"=="--types" set TEST_TYPES=%~2& shift& shift& goto parse_args
if /i "%~1"=="-p" set PARALLEL=%~2& shift& shift& goto parse_args
if /i "%~1"=="--parallel" set PARALLEL=%~2& shift& shift& goto parse_args
if /i "%~1"=="-c" set COVERAGE=%~2& shift& shift& goto parse_args
if /i "%~1"=="--coverage" set COVERAGE=%~2& shift& shift& goto parse_args
if /i "%~1"=="-v" set VERBOSITY=%~2& shift& shift& goto parse_args
if /i "%~1"=="--verbosity" set VERBOSITY=%~2& shift& shift& goto parse_args
if /i "%~1"=="-ff" set FAIL_FAST=true& shift& goto parse_args
if /i "%~1"=="--fail-fast" set FAIL_FAST=true& shift& goto parse_args
if /i "%~1"=="-o" set OUTPUT_DIR=%~2& shift& shift& goto parse_args
if /i "%~1"=="--output" set OUTPUT_DIR=%~2& shift& shift& goto parse_args
if /i "%~1"=="-e" set ENVIRONMENT=%~2& shift& shift& goto parse_args
if /i "%~1"=="--environment" set ENVIRONMENT=%~2& shift& shift& goto parse_args
if /i "%~1"=="-h" goto show_help
if /i "%~1"=="--help" goto show_help
echo 错误: 未知选项 %~1
goto show_help

:end_parse

echo ===============================================
echo        Occop 自动化测试运行器
echo ===============================================
echo.

REM 根据环境调整配置
if not "%ENVIRONMENT%"=="" (
    echo 使用环境配置: %ENVIRONMENT%
    if /i "%ENVIRONMENT%"=="development" (
        set TEST_TYPES=Unit,Integration
        set PARALLEL=true
        set VERBOSITY=normal
    ) else if /i "%ENVIRONMENT%"=="ci" (
        set TEST_TYPES=All
        set PARALLEL=true
        set VERBOSITY=minimal
        set COVERAGE=true
    ) else if /i "%ENVIRONMENT%"=="production" (
        set TEST_TYPES=Integration,Performance,Security
        set PARALLEL=false
        set VERBOSITY=detailed
        set FAIL_FAST=true
    ) else if /i "%ENVIRONMENT%"=="stability" (
        set TEST_TYPES=Stability
        set PARALLEL=false
        set VERBOSITY=detailed
    ) else (
        echo 错误: 未知环境 %ENVIRONMENT%
        exit /b 1
    )
)

echo 测试配置:
echo   测试类型: %TEST_TYPES%
echo   并行运行: %PARALLEL%
echo   覆盖率收集: %COVERAGE%
echo   详细级别: %VERBOSITY%
echo   快速失败: %FAIL_FAST%
echo   输出目录: %OUTPUT_DIR%
echo.

REM 检查.NET是否已安装
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo 错误: 未找到dotnet命令
    echo 请先安装 .NET SDK
    exit /b 1
)

REM 显示.NET版本
echo 环境信息:
dotnet --version
echo.

REM 清理之前的测试结果
if exist "%OUTPUT_DIR%" (
    echo 清理之前的测试结果...
    rmdir /s /q "%OUTPUT_DIR%"
)

REM 创建输出目录
mkdir "%OUTPUT_DIR%"

REM 构建命令参数
set ARGS=--types %TEST_TYPES% --parallel %PARALLEL% --coverage %COVERAGE% --verbosity %VERBOSITY% --output %OUTPUT_DIR%
if "%FAIL_FAST%"=="true" set ARGS=%ARGS% --fail-fast

REM 运行测试运行器
echo 开始运行测试...
echo.

cd /d "%~dp0\.."

dotnet run --project tests\Occop.TestRunner\Occop.TestRunner.csproj -- %ARGS%

if %errorlevel% equ 0 (
    echo.
    echo ===============================================
    echo ✓ 所有测试通过!
    echo ===============================================
    set EXIT_CODE=0
) else (
    echo.
    echo ===============================================
    echo ✗ 测试失败!
    echo ===============================================
    set EXIT_CODE=1
)

REM 显示报告位置
echo.
echo 测试报告已生成:
echo   文本报告: %OUTPUT_DIR%\Reports\TestReport.txt
echo   Markdown报告: %OUTPUT_DIR%\Reports\TestReport.md
echo   HTML报告: %OUTPUT_DIR%\Reports\TestReport.html
echo   JSON报告: %OUTPUT_DIR%\Reports\TestReport.json

if "%COVERAGE%"=="true" (
    echo.
    echo 覆盖率报告:
    dir /s /b "%OUTPUT_DIR%\*Coverage*\index.html" 2>nul
)

echo.
exit /b %EXIT_CODE%

:show_help
echo 用法: %~nx0 [选项]
echo.
echo 选项:
echo   -t, --types ^<types^>        测试类型 (Unit,Integration,Performance,Security,Stability,All)
echo                              默认: All
echo   -p, --parallel ^<bool^>      并行运行 (true/false)
echo                              默认: true
echo   -c, --coverage ^<bool^>      生成覆盖率 (true/false)
echo                              默认: true
echo   -v, --verbosity ^<level^>    详细级别 (quiet,minimal,normal,detailed,diagnostic)
echo                              默认: normal
echo   -ff, --fail-fast           快速失败模式
echo   -o, --output ^<dir^>         输出目录
echo                              默认: TestResults
echo   -e, --environment ^<env^>    环境 (development,ci,production,stability)
echo   -h, --help                 显示帮助信息
echo.
echo 示例:
echo   %~nx0                                   # 运行所有测试
echo   %~nx0 -t Unit,Integration               # 仅运行单元和集成测试
echo   %~nx0 -e ci                             # 使用CI环境配置
echo   %~nx0 -t Security -v detailed           # 详细模式运行安全测试
echo   %~nx0 --fail-fast                       # 快速失败模式
exit /b 0
