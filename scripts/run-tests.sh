#!/bin/bash
# Occop 测试运行脚本
# 用于本地开发和CI环境中运行测试

set -e

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 默认配置
TEST_TYPES="All"
PARALLEL="true"
COVERAGE="true"
VERBOSITY="normal"
FAIL_FAST="false"
OUTPUT_DIR="TestResults"

# 显示帮助信息
show_help() {
    echo "用法: $0 [选项]"
    echo ""
    echo "选项:"
    echo "  -t, --types <types>        测试类型 (Unit,Integration,Performance,Security,Stability,All)"
    echo "                             默认: All"
    echo "  -p, --parallel <bool>      并行运行 (true/false)"
    echo "                             默认: true"
    echo "  -c, --coverage <bool>      生成覆盖率 (true/false)"
    echo "                             默认: true"
    echo "  -v, --verbosity <level>    详细级别 (quiet,minimal,normal,detailed,diagnostic)"
    echo "                             默认: normal"
    echo "  -ff, --fail-fast           快速失败模式"
    echo "  -o, --output <dir>         输出目录"
    echo "                             默认: TestResults"
    echo "  -e, --environment <env>    环境 (development,ci,production,stability)"
    echo "  -h, --help                 显示帮助信息"
    echo ""
    echo "示例:"
    echo "  $0                                  # 运行所有测试"
    echo "  $0 -t Unit,Integration              # 仅运行单元和集成测试"
    echo "  $0 -e ci                            # 使用CI环境配置"
    echo "  $0 -t Security -v detailed          # 详细模式运行安全测试"
    echo "  $0 --fail-fast                      # 快速失败模式"
}

# 解析命令行参数
while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--types)
            TEST_TYPES="$2"
            shift 2
            ;;
        -p|--parallel)
            PARALLEL="$2"
            shift 2
            ;;
        -c|--coverage)
            COVERAGE="$2"
            shift 2
            ;;
        -v|--verbosity)
            VERBOSITY="$2"
            shift 2
            ;;
        -ff|--fail-fast)
            FAIL_FAST="true"
            shift
            ;;
        -o|--output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            echo -e "${RED}错误: 未知选项 $1${NC}"
            show_help
            exit 1
            ;;
    esac
done

# 获取脚本目录
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$SCRIPT_DIR/../.."

echo -e "${BLUE}===============================================${NC}"
echo -e "${BLUE}       Occop 自动化测试运行器${NC}"
echo -e "${BLUE}===============================================${NC}"
echo ""

# 根据环境调整配置
if [ ! -z "$ENVIRONMENT" ]; then
    echo -e "${YELLOW}使用环境配置: $ENVIRONMENT${NC}"
    case $ENVIRONMENT in
        development)
            TEST_TYPES="Unit,Integration"
            PARALLEL="true"
            VERBOSITY="normal"
            ;;
        ci)
            TEST_TYPES="All"
            PARALLEL="true"
            VERBOSITY="minimal"
            COVERAGE="true"
            ;;
        production)
            TEST_TYPES="Integration,Performance,Security"
            PARALLEL="false"
            VERBOSITY="detailed"
            FAIL_FAST="true"
            ;;
        stability)
            TEST_TYPES="Stability"
            PARALLEL="false"
            VERBOSITY="detailed"
            ;;
        *)
            echo -e "${RED}错误: 未知环境 $ENVIRONMENT${NC}"
            exit 1
            ;;
    esac
fi

echo -e "${GREEN}测试配置:${NC}"
echo "  测试类型: $TEST_TYPES"
echo "  并行运行: $PARALLEL"
echo "  覆盖率收集: $COVERAGE"
echo "  详细级别: $VERBOSITY"
echo "  快速失败: $FAIL_FAST"
echo "  输出目录: $OUTPUT_DIR"
echo ""

# 检查.NET是否已安装
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}错误: 未找到dotnet命令${NC}"
    echo "请先安装 .NET SDK"
    exit 1
fi

# 显示.NET版本
echo -e "${GREEN}环境信息:${NC}"
dotnet --version
echo ""

# 清理之前的测试结果
if [ -d "$OUTPUT_DIR" ]; then
    echo -e "${YELLOW}清理之前的测试结果...${NC}"
    rm -rf "$OUTPUT_DIR"
fi

# 创建输出目录
mkdir -p "$OUTPUT_DIR"

# 构建命令参数
ARGS=""
ARGS="$ARGS --types $TEST_TYPES"
ARGS="$ARGS --parallel $PARALLEL"
ARGS="$ARGS --coverage $COVERAGE"
ARGS="$ARGS --verbosity $VERBOSITY"
ARGS="$ARGS --output $OUTPUT_DIR"

if [ "$FAIL_FAST" = "true" ]; then
    ARGS="$ARGS --fail-fast"
fi

# 运行测试运行器
echo -e "${GREEN}开始运行测试...${NC}"
echo ""

cd "$PROJECT_ROOT"

if dotnet run --project tests/Occop.TestRunner/Occop.TestRunner.csproj -- $ARGS; then
    EXIT_CODE=0
    echo ""
    echo -e "${GREEN}===============================================${NC}"
    echo -e "${GREEN}✓ 所有测试通过!${NC}"
    echo -e "${GREEN}===============================================${NC}"
else
    EXIT_CODE=1
    echo ""
    echo -e "${RED}===============================================${NC}"
    echo -e "${RED}✗ 测试失败!${NC}"
    echo -e "${RED}===============================================${NC}"
fi

# 显示报告位置
echo ""
echo -e "${BLUE}测试报告已生成:${NC}"
echo "  文本报告: $OUTPUT_DIR/Reports/TestReport.txt"
echo "  Markdown报告: $OUTPUT_DIR/Reports/TestReport.md"
echo "  HTML报告: $OUTPUT_DIR/Reports/TestReport.html"
echo "  JSON报告: $OUTPUT_DIR/Reports/TestReport.json"

if [ "$COVERAGE" = "true" ]; then
    echo ""
    echo -e "${BLUE}覆盖率报告:${NC}"
    find "$OUTPUT_DIR" -name "index.html" -path "*/Coverage/*" | while read report; do
        echo "  $report"
    done
fi

echo ""

exit $EXIT_CODE
