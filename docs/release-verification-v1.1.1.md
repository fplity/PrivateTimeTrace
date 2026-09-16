# v1.1.1 发布验证记录

日期：2026-09-15。验证由主会话直接执行；本记录是本次图表修复的发布证据，不是完整功能独立回归或第三方安全审计。

## 变更范围

日 / 月趋势图最多 31 个点时完整适配卡片宽度，横轴标签按空间抽稀但保留所有数据点和首尾标签。超过 31 个趋势点或超过 12 个主题分类时仍允许横向滚动。v1.1.1 同时更新应用版本 `1.1.1` 和 Package 版本 `1.1.1.0`。

## 验证结果

| 检查 | 结果 | 证据 / 边界 |
| --- | --- | --- |
| 核心数据与统计 | PASS，23 项 | 新增 23:00 单条 14 分钟日记录，以及 28 / 29 / 30 / 31 天月份的月末单条 14 分钟记录检查 |
| 图表末端可见性 | PASS，16 组合 | Day / Month × Line / Bar × Liquid / Frosted × 1380 / 950；UI Automation 确认末端数据点完全位于 viewport 内、无横向滚动，截图点位检测到蓝色像素 |
| v1.1.0 回归基线 | 按预期 FAIL | 同一回归脚本复现 `Day chart still requires horizontal scrolling.`，用于证明修复针对原问题 |
| 安装器无 UI 检查 | PASS | `verify-installer.ps1 -SkipUi`；安装许可 / 图标 / 菜单、运行时拒绝覆盖、升级数据保留、卸载数据保留 |
| v1.1.0 → v1.1.1 升级 | PASS | 安装器退出码 0；记录数及内容指纹一致；实际安装 DLL 与 16 组合测试包 SHA-256 一致 |
| 安装后实际窗口 | PASS | 主会话目视核对实际安装目录启动的日折线图，00–23 轴和非零峰值均显示 |

16 组合的逐项结果和截图保留在本机验证目录 `artifacts/qa/chart-visibility-v111-3/`；真实数据安装截图仅保留本机，未纳入仓库或 Release。公开辅助截图使用隔离 fixture：

- [日趋势图 fixture 截图](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/screenshots/chart-fix-day-line-frosted.png)
- [月趋势图 fixture 截图](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/screenshots/chart-fix-month-bar-liquid.png)

## 发布资产

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `PrivateTimeTrace-1.1.1-win-x64-Setup.exe` | 93,382,107 bytes | `6f77f5d9e4876f40ce3f3f6f862a4c9708858e75a20d12d844ab6908cf2e0564` |
| `PrivateTimeTrace-1.1.1-win-x64.zip` | 97,733,952 bytes | `c46274ee954c42fcb77e2272c0fec2cc715bae754d44b4bdea24284dd48c1952` |
| `SHA256SUMS.txt` | — | 对上述两个发布文件提供校验清单 |

安装器和 ZIP 是 Windows x64 自包含分发。当前没有商业 Authenticode 签名，Windows 可能显示未知发布者；SHA-256 用于校验下载内容一致性，不替代发布者身份认证。发布资产仅包含安装器、ZIP 和校验清单，不包含数据库、日志、调试文件或真实数据截图。

## 未覆盖范围

本次不是完整 UI 回归，不能据此宣称全部功能已独立测试。未覆盖所有 Windows 10 版本、ARM64 / x86、全部显卡、高对比度、远程桌面、触屏、其他输入设备或长期运行稳定性。项目仍不提供云同步、账号、自动更新或在线学习数据服务。

完整发布说明见 [v1.1.1 发布说明](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/release-notes-v1.1.1.md)，构建流程见 [Windows 发布指南](https://github.com/fplity/PrivateTimeTrace/blob/main/docs/releasing.md)。
