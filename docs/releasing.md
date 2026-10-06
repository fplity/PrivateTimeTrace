# Windows 构建与发布指南

本指南说明时间迹的 Windows x64 自包含分发流程。当前公开版本为 [v1.1.1](https://github.com/fplity/PrivateTimeTrace/releases/tag/v1.1.1)，使用当前用户级 NSIS 安装器；版本信息以 `PrivateTimeTrace.csproj`、`Package.appxmanifest` 和 Release 为准。

## 构建前准备

- Windows x64、PowerShell 7、`global.json` 指定的 .NET SDK。
- 项目源码及首次还原所需的 NuGet 网络访问。
- 已登录的交互桌面，用于实际 UI、图表和安装验证。
- 独立测试数据路径；测试窗口不使用个人学习数据库。

新版本需要同步修改项目的 `Version`、`AssemblyVersion`、`FileVersion`，以及 `Package.appxmanifest` 的 Identity 版本，并更新 `CHANGELOG.md`。应用版本使用三段数字，程序集及 manifest 使用对应四段版本。仅文档修改不需要创建新的应用版本或重新上传同版本安装包。

## 构建发行包

在项目根目录执行：

```powershell
pwsh -NoProfile -File tools/build-release.ps1
```

脚本按项目版本生成默认输出目录，当前为 `artifacts/releases/v1.1.1/`。重复构建需使用新的目录，例如：

```powershell
pwsh -NoProfile -File tools/build-release.ps1 -OutputDirectory "$PWD\artifacts\releases\v1.1.1-rebuild"
```

输出目录必须位于项目的 `artifacts/` 下，且事先不存在。脚本拒绝删除或覆盖已有产物。

构建流程：

1. 执行 `tests/PrivateTimeTrace.Checks` 核心检查。
2. 使用 `--locked-mode` 还原 Windows x64 依赖。
3. 生成 .NET 与 Windows App SDK 自包含程序。
4. 收集实际解析依赖与运行组件的原始许可和版权声明。
5. 检查发行目录中没有数据库、凭据、证书、调试符号或日志。
6. 生成 NSIS 安装器及免安装 ZIP。
7. 为安装器和 ZIP 生成 `SHA256SUMS.txt`。

NSIS `3.12` 由 `tools/bootstrap-nsis.ps1` 下载到项目 `.tools/` 并核对固定 SHA-256，安装器采用 zlib 压缩。镜像下载失败时应检查来源并重试，不跳过校验。

依赖调整后，通过正常还原更新 `packages.lock.json`，复核版本与许可。`tools/collect-licenses.ps1` 从实际包、运行组件和规定来源收集原文；遇到不匹配的许可会停止，不将新依赖默认声明为 MIT。

## 输出文件

```text
artifacts/releases/v1.1.1/
├── PrivateTimeTrace-1.1.1-win-x64-Setup.exe
├── PrivateTimeTrace-1.1.1-win-x64.zip
├── SHA256SUMS.txt
└── work/
    └── app/   自包含程序与随包许可，供验证使用
```

公开 Release 只上传安装器、ZIP 与校验清单。构建工作目录、QA 数据库、真实学习数据、日志、调试文件和证书不上传。经过检查且使用隔离演示数据的截图可作为文档素材保存在 `docs/screenshots/`。

## 运行验证

下面以新生成的默认目录为例；验证实际候选包时，将 `$releaseDirectory` 改成那个包的路径。v1.1.1 历史发布构建在 `artifacts/releases/v1.1.1-chart-fix/`，它不保证存在于新克隆仓库中。

```powershell
$releaseDirectory = Join-Path $PWD 'artifacts\releases\v1.1.1'
$candidateExe = Join-Path $releaseDirectory 'work\app\PrivateTimeTrace.exe'
$candidateInstaller = Join-Path $releaseDirectory 'PrivateTimeTrace-1.1.1-win-x64-Setup.exe'
```

### 核心行为

```powershell
dotnet run --project tests/PrivateTimeTrace.Checks/PrivateTimeTrace.Checks.csproj -c Release
```

核心检查不打开 WinUI 窗口，使用隔离临时数据库。主要覆盖时间边界、统计一致性、主题合并、偏好、活动计时、原子完成、删除与初始化兼容。

### 实际 UI

```powershell
pwsh -NoProfile -File tools/verify-ui.ps1 -ExePath $candidateExe -FixturePath "$PWD\artifacts\qa\candidate-ui-new.db"
```

检查两套风格、两个图表的独立选择、偏好恢复、计时开始／恢复／结束、周期操作及窄窗口。指定的 fixture 需为 `artifacts/qa/` 下尚不存在的新文件，脚本拒绝覆盖已有数据。

### 日／月图表可见性

```powershell
pwsh -NoProfile -File tools/verify-chart-visibility.ps1 -ExePath $candidateExe -OutputDirectory "$PWD\artifacts\qa\candidate-chart-new"
```

检查 16 个日／月、折线／柱状、风格及窗口宽度组合。使用单条位于 23 点或月末的隔离记录，确认末端数据标记完全位于可视区域内、无横向滚动，并核对实际截图像素。输出目录必须是 `artifacts/qa/` 下的新子目录。

### 按钮动效

`tools/verify-liquid-motion.ps1` 提供切换逐帧、局部光影、静态内容、快速反向切换、键盘选择和退出验证。使用新的证据目录执行：

```powershell
pwsh -NoProfile -File tools/verify-liquid-motion.ps1 -ExePath $candidateExe -OutputDirectory "$PWD\artifacts\qa\candidate-motion-new"
```

动效范围保持在按钮内部：选择胶囊可滑动、拉伸、轻微过冲和回弹，光影跟随局部指针。卡片、壁纸和图表内容不加入全局跟随或视差，发布说明应准确描述这一范围。

### 隔离安装器

```powershell
pwsh -NoProfile -File tools/verify-installer.ps1 -InstallerPath $candidateInstaller
```

默认流程包含安装后的完整 UI 检查。若完整 UI 已单独执行，可通过 `-SkipUi` 执行安装器行为子集，但在报告中要分别写明两类证据，不能把 `-SkipUi` 称为完整 UI 验证。

```powershell
pwsh -NoProfile -File tools/verify-installer.ps1 -InstallerPath $candidateInstaller -SkipUi
```

测试使用独立的 `PrivateTimeTrace.InstallTest` 安装目录、卸载注册项、菜单和隔离数据库。已有测试目标不为空时脚本停止，需要先检查上次残留；不能用清空宽泛目录的方式解决。

## 发布验收记录

最终验收至少说明：

- 版本、受测程序与安装包的确切路径、文件大小及 SHA-256。
- 核心行为、实际窗口、独立图表选择、偏好恢复、窗口尺寸与受影响功能。
- 安装、占用保护、覆盖升级、卸载以及学习数据和额外文件保留。
- 系统环境、签名状态、未覆盖范围及发现的限制。
- 截图是概念设计、隔离测试窗口还是实际安装窗口；公开素材是否含个人数据。

候选包的证据必须对应最终上传的文件。仅编译成功、进程启动或按钮状态变化，不足以证明实际窗口内容正确。历史证据见 [v1.1.1 验证记录](release-verification-v1.1.1.md)、[首版发布验证](release-verification.md)和[UI 验证记录](verification-report.md)。

## 安装、升级与卸载

普通安装显示许可、组件选择和完成页面，固定安装在当前用户的 `%LOCALAPPDATA%\Programs\PrivateTimeTrace`。不写入 HKLM、不建立服务、不要求管理员权限。开始菜单提供入口，桌面快捷方式为可选项。

静默安装需要调用方已经阅读并接受随包适用许可：

```powershell
.\PrivateTimeTrace-1.1.1-win-x64-Setup.exe /S /ACCEPTLICENSES
```

| 退出码 | 含义 |
| --- | --- |
| `0` | 完成对应操作 |
| `2` | 系统不是 x64，或系统版本低于安装器配置下限 |
| `3` | 静默安装未提供许可接受参数 |
| `4` | 另一个时间迹安装程序正在运行 |
| `5` | 安装目标标记无效，或无标记的非空目录存在其他文件 |
| `6` | 目标程序正在运行，拒绝覆盖或卸载 |
| `7` | 安装文件写入失败 |

更新前退出所有应用窗口，按需备份学习数据。安装器不会强制结束正在运行的程序。卸载使用 Windows「已安装的应用」或开始菜单入口，按包内清单删除文件及专用快捷方式、注册项；保留额外文件和独立的学习数据目录 `%LOCALAPPDATA%\PrivateTimeTrace`。

`/TESTINSTALL` 仅供隔离安装验证。测试进程必须指定测试用 `--data-file`，不以默认启动方式向个人数据库写入演示记录。

## GitHub 发布

确认最终包和证据后，提交源码与文档，创建对应版本标签，再发布 Release。说明中包含用户可理解的问题与改进、下载文件、哈希、数据行为、许可和验证范围；Release 的文档链接使用完整 GitHub URL。

当前发行安装器未进行 Authenticode 签名；不要将 SHA-256 描述为发布者身份认证。GitHub 自动生成的源码压缩包不应称作安装包。

GitHub Actions 使用仓库只读权限，在 main 推送、版本标签、Pull Request 和手动触发时构建并归档 CI 产物。工作流不自动创建正式 Release。已经公开的版本应保留资产与标签，修复程序后使用新的版本号发布。
