# 第三方许可与分发声明

时间迹原创代码采用根目录的 MIT 许可证。此许可不覆盖第三方组件的独立权利，不移除原作者的版权、专利、商标或其他条件。

安装程序会展示应用许可及附带的微软组件条款。安装或使用二进制分发前，请阅读随包提供的 `licenses/` 原文。就微软可分发代码而言，分发者及外部终端用户须遵守适用于对应组件的原始条款；本项目的 MIT 授权不替代这些条款，也不限制第三方开源许可证已授予的权利。

## 组件

| 组件 | 说明与许可位置 |
| --- | --- |
| CommunityToolkit.Mvvm | MIT；保留原始 License.md 和 ThirdPartyNotices.txt |
| Microsoft.Data.Sqlite / Core | 来自 EF Core，MIT；保留微软版权及许可 |
| SQLitePCLRaw | Apache-2.0；Copyright 2014-2024 SourceGear, LLC |
| SQLite | SQLite 引擎本身为公有领域；包装与绑定层仍须遵循各自许可 |
| .NET Windows Runtime | 保留运行包的 MIT 源码许可、第三方声明，以及 Windows 二进制的 Microsoft .NET Library 条款 |
| Windows App SDK / WinUI / Windows SDK 投影 | 依各 NuGet 包及微软 Windows SDK 原始许可分发，不将微软二进制笼统称作 MIT |
| WebView2、Windows ML / AI、System.Numerics.Tensors 等传递依赖 | 按构建实际解析结果收集各自原文；存在依赖不代表本应用启用了相关功能 |
| NSIS 3.12 | 安装器采用 zlib 压缩；完整 COPYING 随包提供，包含相关第三方条款 |

精确版本、包用途、原始声明路径及 SHA-256 见 [依赖清单](licenses/dependencies.json)和[可读索引](licenses/README.md)。清单包含构建期组件，已单独标注，不表示这些构建工具全部装到终端用户电脑上。

## 原始许可来源

- NuGet 包：读取实际还原版本内的 LICENSE、NOTICE、ThirdPartyNotices 和 nuspec，不改写内容。
- [EF Core v10.0.0 MIT 原文](https://github.com/dotnet/efcore/blob/v10.0.0/LICENSE.txt)。
- [SQLitePCLRaw 的 Apache-2.0 原文](https://github.com/ericsink/SQLitePCL.raw/blob/v2.1.12/LICENSE.TXT)：用作声明 Apache-2.0 的 2.1.13 NuGet 包所需标准许可文本，精确包版权另保留于 nuspec 和清单。
- [WinApp CLI v0.6.1 MIT 原文](https://github.com/microsoft/WinAppCli/blob/v0.6.1/LICENSE)。
- [Microsoft Windows SDK 许可](https://aka.ms/WinSDKLicenseURL)。
- [Microsoft .NET Library 许可](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm)及 [.NET 分发许可说明](https://github.com/dotnet/core/blob/main/license-information.md)。
- [NSIS 原始许可](https://nsis.sourceforge.io/License)。

微软字体由操作系统提供，项目不打包字体文件。Apple、Microsoft、Windows、.NET 等名称和商标属于各自权利人。本项目不宣称获得这些公司的认证或背书。
