#!/bin/bash
# Occop 快速构建脚本 (Linux/macOS)
# 注意：此脚本用于跨平台构建Windows可执行文件

set -e  # 遇到错误立即退出

echo "========================================"
echo "      Occop 构建和发布工具"
echo "========================================"
echo ""

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 检查dotnet
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}[错误]${NC} 未找到dotnet命令"
    echo "请先安装 .NET 6.0 SDK"
    echo "下载地址: https://dotnet.microsoft.com/download/dotnet/6.0"
    exit 1
fi

echo -e "${GREEN}[信息]${NC} 检测到 .NET SDK:"
dotnet --version
echo ""

# 设置变量
SOLUTION="src/Occop.sln"
PROJECT="src/Occop.UI/Occop.UI.csproj"
OUTPUT="publish/win-x64"
CONFIG="Release"

echo "========================================"
echo "构建配置:"
echo "  解决方案: $SOLUTION"
echo "  主项目:   $PROJECT"
echo "  输出目录: $OUTPUT"
echo "  构建配置: $CONFIG"
echo "========================================"
echo ""

read -p "按Enter继续..."

# 步骤1: 清理
echo ""
echo -e "${YELLOW}[1/5]${NC} 清理旧构建文件..."
echo "----------------------------------------"
dotnet clean "$SOLUTION" --configuration "$CONFIG"
if [ -d "$OUTPUT" ]; then
    echo "删除旧的发布文件..."
    rm -rf "$OUTPUT"
fi
echo -e "${GREEN}[完成]${NC} 清理完成"
echo ""

# 步骤2: 还原依赖
echo -e "${YELLOW}[2/5]${NC} 还原NuGet包..."
echo "----------------------------------------"
dotnet restore "$SOLUTION"
echo -e "${GREEN}[完成]${NC} 依赖还原完成"
echo ""

# 步骤3: 构建
echo -e "${YELLOW}[3/5]${NC} 构建项目..."
echo "----------------------------------------"
dotnet build "$SOLUTION" --configuration "$CONFIG" --no-restore
echo -e "${GREEN}[完成]${NC} 构建成功"
echo ""

# 步骤4: 运行测试（可选）
echo -e "${YELLOW}[4/5]${NC} 运行测试（可选）..."
echo "----------------------------------------"
read -p "是否运行测试? (y/N): " run_tests
if [[ "$run_tests" =~ ^[Yy]$ ]]; then
    if dotnet test "$SOLUTION" --configuration "$CONFIG" --no-build --verbosity normal; then
        echo -e "${GREEN}[完成]${NC} 所有测试通过"
    else
        echo -e "${YELLOW}[警告]${NC} 部分测试失败"
        read -p "是否继续发布? (y/N): " continue_publish
        if [[ ! "$continue_publish" =~ ^[Yy]$ ]]; then
            echo "已取消发布"
            exit 1
        fi
    fi
else
    echo -e "${YELLOW}[跳过]${NC} 测试已跳过"
fi
echo ""

# 步骤5: 发布
echo -e "${YELLOW}[5/5]${NC} 发布应用程序..."
echo "----------------------------------------"
echo "正在生成自包含单文件可执行文件..."
echo "这可能需要几分钟时间..."
echo ""

dotnet publish "$PROJECT" \
  --configuration "$CONFIG" \
  --runtime win-x64 \
  --self-contained true \
  --output "$OUTPUT" \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true \
  /p:PublishReadyToRun=true \
  --no-restore

echo -e "${GREEN}[完成]${NC} 发布成功"
echo ""

# 显示结果
echo "========================================"
echo "          构建完成！"
echo "========================================"
echo ""
echo "可执行文件位置:"
echo "  $OUTPUT/Occop.UI.exe"
echo ""

# 计算文件大小
if [ -f "$OUTPUT/Occop.UI.exe" ]; then
    size=$(stat -f%z "$OUTPUT/Occop.UI.exe" 2>/dev/null || stat -c%s "$OUTPUT/Occop.UI.exe" 2>/dev/null)
    sizeMB=$((size / 1048576))
    echo "文件大小: ${sizeMB} MB"
fi
echo ""

echo "后续步骤:"
echo "  1. 将文件复制到Windows系统测试"
echo "  2. 查看日志配置: src/Occop.UI/nlog.config"
echo "  3. 创建安装程序（可选）"
echo ""

echo -e "${GREEN}构建成功完成！${NC}"
