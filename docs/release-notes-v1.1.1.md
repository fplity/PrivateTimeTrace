# 时间迹 v1.1.1 · 修复日/月图表显示

v1.1.1 修复了日、月趋势图在记录位于时间轴末端时的可见性问题。

## 修复内容

- 修复 24 小时日图或 28–31 天月图被强制撑宽、导致下午或月末数据位于卡片右侧不可见的问题。
- 最多 31 个趋势点时让完整数据适配卡片宽度；横轴标签按可用空间抽稀，同时保留所有数据点和首尾标签。
- 超过 31 个趋势点，或主题分类超过 12 个时，继续保留横向滚动以维持可读性。
- 保留 v1.1.0 的按钮液态交互、双玻璃风格，以及专注趋势 / 主题分布各自独立的折线图与柱状图切换。

## 下载

从 [GitHub Release v1.1.1](https://github.com/fplity/PrivateTimeTrace/releases/tag/v1.1.1) 下载 Windows x64 自包含包：

| 文件 | SHA-256 |
| --- | --- |
| [PrivateTimeTrace-1.1.1-win-x64-Setup.exe](https://github.com/fplity/PrivateTimeTrace/releases/download/v1.1.1/PrivateTimeTrace-1.1.1-win-x64-Setup.exe) | `6f77f5d9e4876f40ce3f3f6f862a4c9708858e75a20d12d844ab6908cf2e0564` |
| [PrivateTimeTrace-1.1.1-win-x64.zip](https://github.com/fplity/PrivateTimeTrace/releases/download/v1.1.1/PrivateTimeTrace-1.1.1-win-x64.zip) | `c46274ee954c42fcb77e2272c0fec2cc715bae754d44b4bdea24284dd48c1952` |

完整校验清单：[SHA256SUMS.txt](https://github.com/fplity/PrivateTimeTrace/releases/download/v1.1.1/SHA256SUMS.txt)。安装器和 ZIP 均未进行商业 Authenticode 签名，Windows 可能显示未知发布者；SHA-256 只用于校验下载内容一致性，不能替代发布者身份认证。

## 验证边界

- 23 项核心检查通过，包括 23:00 的单条 14 分钟日记录，以及 28、29、30、31 天月份的月末单条 14 分钟记录。
- 独立隔离 fixture 的实际窗口检查通过 16 个组合：Day / Month × Line / Bar × Liquid / Frosted × 1380 / 950 宽度。原生 UI Automation 检查末端数据点矩形完全位于 viewport 内、无横向滚动，并从窗口截图检测到实际蓝色数据像素。
- 同一回归脚本在 v1.1.0 上可复现失败：`Day chart still requires horizontal scrolling.`
- `verify-installer.ps1 -SkipUi` 通过，覆盖安装许可 / 图标 / 菜单、运行时拒绝覆盖、升级数据保留和卸载数据保留。
- 从 v1.1.0 升级到 v1.1.1 的安装器退出码为 0；正式数据的记录数及内容指纹一致；实际安装 DLL 与 16 组合测试包的 SHA-256 一致。
- 主会话已目视核对升级后实际安装目录启动的日折线图：00–23 轴和非零峰值均显示。该真实数据截图仅保留本机，没有作为公开资产上传。

本次验证聚焦图表修复、安装升级和相关发布门禁，不是全部功能的独立 UI 回归，也不宣称已覆盖所有 Windows 版本、硬件、输入设备或长期稳定性。项目仍是 Windows x64、本地优先应用，不包含云同步、账号或在线学习数据服务。

更多项目说明见 [README](https://github.com/fplity/PrivateTimeTrace/blob/main/README.md)，完整发布流程见 [Windows 发布指南](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/releasing.md)。

## 隔离验证截图

以下截图来自不含用户记录的隔离 fixture：

![日趋势图末端可见性](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/screenshots/chart-fix-day-line-frosted.png)
![月趋势图末端可见性](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/screenshots/chart-fix-month-bar-liquid.png)

## 许可

项目原创代码采用 [MIT License](https://github.com/fplity/PrivateTimeTrace/blob/main/LICENSE)。随包分发的 .NET、Windows App SDK、SQLite、NSIS 等组件保留各自许可，详见 [第三方声明](https://github.com/fplity/PrivateTimeTrace/blob/main/THIRD_PARTY_NOTICES.md)。
