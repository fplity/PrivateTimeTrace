# 时间迹 v1.1.0

v1.1.0 是一次以交互质感为主的 Windows 桌面更新。它保留 v1.0.0 的本地计时、记录和统计能力，把可见的动效集中在“当前选中了哪个按钮”这一件事上。

## 新增与改进

- **按钮选择更清晰**：风格、导航、统计周期、专注趋势和主题分布的切换按钮使用连续滑动的玻璃选择胶囊。选择位置会根据移动方向产生轻微拉伸、过冲和回弹，让状态切换更容易被感知。
- **按钮内局部高光**：指针移动时，选中按钮内部会出现克制的入射高光、冷色反射、弧形流光和边缘亮线；高光跟随按钮局部坐标，不带动整张卡片或壁纸。
- **两套风格各有取舍**：液态流光保留更明显的流动感，霜白玻璃使用更克制的形变与光影。两套风格仍可随时切换，并在重启后恢复偏好。
- **交互状态保持稳定**：文字、命中区域和页面内容不随形变漂移；鼠标、键盘方向键、Home / End 以及原生辅助功能选中状态保持同步。
- **图表选择继续独立**：专注趋势与主题分布仍分别支持折线图 / 柱状图，互不绑定，选择会自动保存。

## 明确不变的范围

本版本没有加入云同步、账号、联网学习数据服务、广告、自动更新或数据库结构变更。卡片、页面切换、壁纸视差、全局光影和图表内容不加入 v1.1.0 的按钮动效；背景只会在真实数据或风格发生变化时更新。

这是基于 WinUI Composition 的 Windows 原生近似设计，不是 Apple 私有 Liquid Glass 渲染器，也不代表与 Apple 或 Microsoft 存在隶属或背书关系。系统关闭动画或高级视觉效果时，应用会减少动效。

## 下载与运行边界

发布后请从本仓库的 [Releases](https://github.com/fplity/PrivateTimeTrace/releases) 下载对应的 `PrivateTimeTrace-1.1.0-win-x64-Setup.exe`、`PrivateTimeTrace-1.1.0-win-x64.zip` 和 `SHA256SUMS.txt`。安装包和 ZIP 均为 Windows x64 自包含分发，不需要开发者模式或额外 .NET 开发环境；ZIP 必须完整解压后运行。

当前分发包没有商业 Authenticode 签名，Windows 可能显示未知发布者。SHA-256 只能校验下载内容一致性，不能替代发布者身份认证；请核对 Release 来源，不要关闭系统安全防护或导入陌生证书。

数据仍保存在 `%LOCALAPPDATA%\PrivateTimeTrace\private-time-trace.db`，卸载不会主动删除学习数据库。备份或恢复前请退出应用并复制完整数据目录，同时注意可能存在的 `-wal` / `-shm` 文件。数据库没有应用级加密。

## 验证说明

最终 `v1.1.0-final2` 包已完成发布门禁核验：

| 文件 | 大小 | SHA-256 |
| --- | ---: | --- |
| `PrivateTimeTrace-1.1.0-win-x64-Setup.exe` | 93,380,831 bytes | `A1B2C5484CC064EB583DFB29615B063E317484D14B5172811D20D367991EB381` |
| `PrivateTimeTrace-1.1.0-win-x64.zip` | 97,733,590 bytes | `8C84261D95805F2BA2CAA3D4A69FC53461B54879696BEF7373659FAD289227A4` |
| `SHA256SUMS.txt` | 210 bytes | 以上两个下载文件的校验清单 |

- `verify-installer.ps1 -SkipUi`：PASS；该命令不代表完整 UI 回归。
- 从 1.0.0 升级到 1.1.0：安装器退出码 `0`。
- 正式数据库升级前后完全一致：`Count=2`；`RecordsHash=7A2975B4D35A343DB0DB6E6834331724F2FCE885AC3EADB854AF9C4F4B6B9D3`；`HasActiveSession=false`；`ActiveHash=74234E98AFE7498FB5DAF1F36AC2D78ACC339464F950703B8C019892F982B90B`；数据库 SHA-256=`9549444129FE41F4E0B3C3D88BD16350AE94DF0B5F673C945A75AF5881805C62`。
- 安装位置和快捷方式 `TargetPath` 均指向测试机的 `C:\Users\刘煜平\AppData\Local\Programs\PrivateTimeTrace\PrivateTimeTrace.exe`；`IconLocation` 指向 `Assets\AppIcon.ico,0`。
- 完整 UI 回归已有独立通过证据，覆盖按钮局部动效、两套风格、两个图表独立切换、键盘 / 辅助功能选择、偏好重启和窗口缩放。

候选包、隔离数据库、日志、截图和调试文件未作为 Release 资产上传。

完整构建与发布流程见 [Windows 发布指南](https://github.com/fplity/PrivateTimeTrace/blob/v1.1.0/docs/releasing.md)。

## 许可

项目原创代码采用 [MIT License](https://github.com/fplity/PrivateTimeTrace/blob/v1.1.0/LICENSE)。.NET、Windows App SDK、SQLite、NSIS 等随包组件保留各自许可；完整原始条款与声明见 [第三方声明](https://github.com/fplity/PrivateTimeTrace/blob/v1.1.0/THIRD_PARTY_NOTICES.md) 和 [licenses](https://github.com/fplity/PrivateTimeTrace/tree/v1.1.0/licenses)。
