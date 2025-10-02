# Occop 构建和部署指南

## 📋 前置要求

### 必需工具

1. **Windows操作系统**
   - Windows 10 或更高版本
   - x64架构

2. **.NET SDK**
   - 版本：.NET 6.0 SDK或更高
   - 下载：https://dotnet.microsoft.com/download/dotnet/6.0
   - 验证安装：
   ```bash
   dotnet --version
   # 应显示：6.0.x 或更高
   ```

3. **Visual Studio 2022** (推荐) 或 **Visual Studio Code**
   - **Visual Studio 2022**（推荐用于WPF开发）:
     - 社区版(免费)：https://visualstudio.microsoft.com/downloads/
     - 工作负载：选择".NET桌面开发"
   - **VS Code**（轻量级选项）:
     - 下载：https://code.visualstudio.com/
     - 扩展：C# for Visual Studio Code

4. **Git**（已安装）
   - 用于版本控制和获取代码

### 可选工具

- **Windows Terminal**：更好的命令行体验
- **dotnet-coverage**：代码覆盖率工具
- **BenchmarkDotNet**：性能测试（已包含在项目中）

---

## 🔨 方法1: 使用Visual Studio构建（推荐）

### 步骤1: 打开解决方案

1. 启动Visual Studio 2022
2. 点击"打开项目或解决方案"
3. 导航到项目目录并打开 `src/Occop.sln`

### 步骤2: 还原NuGet包

Visual Studio会自动还原包，或手动还原：
- 右键点击解决方案 → "还原NuGet包"
- 或使用菜单：工具 → NuGet包管理器 → 管理解决方案的NuGet包

### 步骤3: 选择构建配置

在工具栏选择：
- **Debug**: 开发和测试
- **Release**: 生产部署

### 步骤4: 构建项目

- 快捷键：`Ctrl+Shift+B`
- 或菜单：生成 → 生成解决方案

### 步骤5: 运行应用程序

- 快捷键：`F5`（调试模式）或 `Ctrl+F5`（无调试）
- 或点击工具栏的"启动"按钮

---

## 🚀 方法2: 使用命令行构建

### 步骤1: 打开命令提示符/PowerShell

导航到项目根目录：
```bash
cd /path/to/occop
```

### 步骤2: 还原依赖

```bash
dotnet restore src/Occop.sln
```

### 步骤3: 构建项目

**Debug构建**:
```bash
dotnet build src/Occop.sln --configuration Debug
```

**Release构建**:
```bash
dotnet build src/Occop.sln --configuration Release
```

### 步骤4: 运行应用程序

```bash
dotnet run --project src/Occop.UI/Occop.UI.csproj
```

---

## 📦 发布部署包

### 方法A: 自包含部署（推荐）

生成包含.NET运行时的独立可执行文件：

```bash
dotnet publish src/Occop.UI/Occop.UI.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output ./publish/win-x64 \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true
```

**参数说明**:
- `--runtime win-x64`: 目标Windows 64位
- `--self-contained true`: 包含.NET运行时
- `PublishSingleFile=true`: 生成单个可执行文件
- `IncludeNativeLibrariesForSelfExtract=true`: 包含本机库
- `EnableCompressionInSingleFile=true`: 压缩文件

**输出位置**: `publish/win-x64/Occop.UI.exe`

### 方法B: 框架依赖部署（体积更小）

要求目标机器已安装.NET 6.0运行时：

```bash
dotnet publish src/Occop.UI/Occop.UI.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained false \
  --output ./publish/win-x64-framework-dependent
```

**输出位置**: `publish/win-x64-framework-dependent/Occop.UI.exe`

### 方法C: 使用Visual Studio发布

1. 右键点击 `Occop.UI` 项目
2. 选择"发布..."
3. 选择目标：文件夹
4. 配置选项：
   - 目标运行时：win-x64
   - 部署模式：独立
   - 目标框架：net6.0-windows
   - 文件发布选项：✓ 生成单个文件
5. 点击"发布"

---

## 🧪 运行测试

### 运行所有测试

```bash
dotnet test src/Occop.sln --configuration Release
```

### 运行特定测试项目

```bash
# 单元测试
dotnet test tests/Occop.Tests/Occop.Tests.csproj

# 集成测试
dotnet test tests/Occop.IntegrationTests/Occop.IntegrationTests.csproj

# 性能测试
dotnet test tests/Occop.PerformanceTests/Occop.PerformanceTests.csproj

# 安全测试
dotnet test tests/Occop.SecurityTests/Occop.SecurityTests.csproj

# 稳定性测试
dotnet test tests/Occop.StabilityTests/Occop.StabilityTests.csproj
```

### 使用TestRunner

```bash
dotnet run --project tests/Occop.TestRunner/Occop.TestRunner.csproj -- --types All
```

### 生成测试覆盖率报告

```bash
dotnet test src/Occop.sln \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

---

## 📁 部署包结构

发布后的目录结构：

```
publish/win-x64/
├── Occop.UI.exe              # 主程序
├── appsettings.json          # 应用配置（如果有）
├── nlog.config              # 日志配置
└── (其他依赖文件)
```

---

## 🔧 配置应用程序

### 1. 日志配置

编辑 `src/Occop.UI/nlog.config`:

```xml
<!-- 调整日志级别 -->
<rules>
  <logger name="*" minlevel="Info" writeTo="allfile" />
</rules>

<!-- 修改日志文件路径 -->
<target xsi:type="File" name="allfile"
  fileName="C:/Logs/Occop/occop-${shortdate}.log" />
```

### 2. 应用设置

如果有 `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "GitHub": {
    "ClientId": "your-client-id"
  }
}
```

---

## 📦 创建安装程序（可选）

### 使用WiX Toolset

1. 安装WiX Toolset: https://wixtoolset.org/
2. 创建安装项目
3. 配置产品信息、快捷方式等
4. 构建MSI安装包

### 使用Inno Setup（推荐）

1. 下载Inno Setup: https://jrsoftware.org/isinfo.php
2. 创建安装脚本 `setup.iss`:

```ini
[Setup]
AppName=Occop
AppVersion=1.0.0
DefaultDirName={pf}\Occop
DefaultGroupName=Occop
OutputBaseFilename=Occop-Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Occop"; Filename: "{app}\Occop.UI.exe"
Name: "{commondesktop}\Occop"; Filename: "{app}\Occop.UI.exe"

[Run]
Filename: "{app}\Occop.UI.exe"; Description: "Launch Occop"; Flags: postinstall nowait skipifsilent
```

3. 编译安装脚本生成 `Occop-Setup.exe`

---

## 🚨 常见问题

### 问题1: "找不到.NET运行时"

**解决方案**:
- 下载并安装.NET 6.0 Desktop Runtime
- 或使用自包含部署

### 问题2: "无法启动应用程序"

**解决方案**:
```bash
# 检查依赖
dotnet --info

# 清理并重新构建
dotnet clean src/Occop.sln
dotnet build src/Occop.sln --configuration Release
```

### 问题3: "NuGet包还原失败"

**解决方案**:
```bash
# 清除NuGet缓存
dotnet nuget locals all --clear

# 重新还原
dotnet restore src/Occop.sln
```

### 问题4: "缺少本机库"

**解决方案**:
- 确保安装了Visual C++ Redistributable
- 下载：https://aka.ms/vs/17/release/vc_redist.x64.exe

---

## 📊 性能优化

### Release构建优化

在 `.csproj` 文件中添加：

```xml
<PropertyGroup Condition="'$(Configuration)'=='Release'">
  <Optimize>true</Optimize>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
  <TieredCompilation>true</TieredCompilation>
  <TieredCompilationQuickJit>true</TieredCompilationQuickJit>
</PropertyGroup>
```

### ReadyToRun (R2R) 编译

```bash
dotnet publish src/Occop.UI/Occop.UI.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  /p:PublishReadyToRun=true \
  /p:PublishSingleFile=true
```

---

## 🔐 签名和验证（可选）

### 代码签名

使用证书签名可执行文件：

```bash
signtool sign /f mycert.pfx /p password /t http://timestamp.digicert.com publish/win-x64/Occop.UI.exe
```

---

## 📋 部署清单

- [ ] .NET 6.0 SDK已安装
- [ ] 所有NuGet包已还原
- [ ] 解决方案成功构建（Release配置）
- [ ] 所有测试通过
- [ ] 应用程序配置正确
- [ ] 发布包已生成
- [ ] 在目标环境中测试
- [ ] 创建安装程序（如需要）
- [ ] 准备用户文档
- [ ] 设置自动更新机制（如需要）

---

## 🎯 快速开始脚本

将以下内容保存为 `build.bat`:

```batch
@echo off
echo ====================================
echo Occop 构建脚本
echo ====================================

echo.
echo [1/4] 清理旧文件...
dotnet clean src\Occop.sln

echo.
echo [2/4] 还原依赖...
dotnet restore src\Occop.sln

echo.
echo [3/4] 构建项目...
dotnet build src\Occop.sln --configuration Release

echo.
echo [4/4] 发布应用...
dotnet publish src\Occop.UI\Occop.UI.csproj ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  --output .\publish\win-x64 ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true

echo.
echo ====================================
echo 构建完成！
echo 输出位置: publish\win-x64\Occop.UI.exe
echo ====================================
pause
```

使用：双击 `build.bat` 或在命令行运行。

---

## 📞 获取帮助

如果遇到问题：

1. 检查GitHub Issues: https://github.com/v2sh1t/occop/issues
2. 查看项目文档: `docs/` 目录
3. 运行诊断：
   ```bash
   dotnet --info
   dotnet --list-sdks
   dotnet --list-runtimes
   ```

---

**最后更新**: 2025-10-03
**版本**: 1.0.0
