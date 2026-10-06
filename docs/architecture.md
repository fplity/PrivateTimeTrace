# 时间迹技术架构

本文依据当前 v1.1.1 源码说明实现方式。产品能力见 [README](../README.md)，用户操作见 [使用手册](user-guide.md)，发行流程见 [发布指南](releasing.md)。

## 实现范围

时间迹是单机 Windows 原生应用。C# / .NET 10 承担逻辑与数据操作，WinUI 3 / Windows App SDK 提供窗口和 XAML 控件，CommunityToolkit.Mvvm 提供可观察属性与命令，SQLite 保存本地状态。

当前程序没有账号体系、同步服务或应用级数据库加密；安装版、ZIP 版与未指定数据文件的开发版默认使用同一用户的 LocalAppData 数据库。

## 模块职责

| 文件或模块 | 职责 |
| --- | --- |
| [App.xaml.cs](../App.xaml.cs) | 启动应用、解析 `--data-file`、暴露窗口与调度队列、读取系统效果设置、记录未处理异常 |
| [MainWindow.xaml.cs](../MainWindow.xaml.cs) | 设置窗口尺寸、标题栏、图标和关闭生命周期 |
| [MainPage.xaml](../MainPage.xaml) | 定义概览、记录、分析页面区域，通过 `x:Bind` 连接页面状态 |
| [MainPage.xaml.cs](../MainPage.xaml.cs) | 导航、风格与图表类型事件、主题下钻、删除确认和响应式布局 |
| [MainPageViewModel.cs](../ViewModels/MainPageViewModel.cs) | 初始化数据、活动计时、异步命令、记录集合、分析结果与偏好写入 |
| [StudyDatabase.cs](../Data/StudyDatabase.cs) | SQLite 初始化、参数化读写、活动计时和偏好事务 |
| [StudyAnalytics.cs](../Services/StudyAnalytics.cs) | 纯数据统计：周期范围、时间重叠、趋势分桶、主题聚合、时长格式化 |
| [StudyChart.cs](../Controls/StudyChart.cs) | 用 Canvas / Shapes 绘制坐标轴、折线、面积、柱状、数据点和提示 |
| [GlassPanel.cs](../Controls/GlassPanel.cs) | 玻璃容器、静态层次、Acrylic 与边缘光影 |
| [LiquidSelector.cs](../Controls/LiquidSelector.cs) | 原生 RadioButton 组的共享选择胶囊与连续形变 |
| [LiquidLight.cs](../Controls/LiquidLight.cs) / [Motion.cs](../Controls/Motion.cs) | 控件局部 Composition 光影、悬停和按压反馈 |

页面拥有 UI 事件和布局职责，ViewModel 维护页面状态与业务操作，数据访问集中在 `StudyDatabase`，统计集中在 `StudyAnalytics`。图表消费已经计算好的 `ChartPoint` 集合，不直接查询数据库。

## 数据模型

### 已完成记录：`study_records`

| 字段 | SQLite 类型 | 含义 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 当前数据库内的记录标识 |
| `note` | TEXT NOT NULL | 学习主题，可为空字符串 |
| `started_at` | TEXT NOT NULL | UTC 开始时间，使用往返格式字符串 |
| `ended_at` | TEXT NOT NULL | UTC 结束时间，使用往返格式字符串 |

时长由结束与开始时间换算到 UTC 后相减得到，不单独存储可变的累计值。读取时转换为本地时间供界面和自然周期分析使用。新增记录会检查结束时间晚于开始时间；SQL 参数避免将主题拼接到语句中。

### 活动计时：`active_session`

表包含固定 `id = 1`、主题和开始时间，最多保存一条活动计时。开始操作使用冲突时不覆盖的插入方式，保留已经存在的活动计时。UI 不提供暂停状态或同时运行多个主题的入口。

### 显示偏好：`app_settings`

| 键 | 保存内容 | 默认值 |
| --- | --- | --- |
| `style` | `Liquid` / `Frosted` | `Liquid` |
| `trend_kind` | 趋势图 `Line` / `Bar` | `Line` |
| `topic_kind` | 主题图 `Line` / `Bar` | `Bar` |

未知枚举值回退到默认设置。统计周期、导航页面与未开始的主题输入不属于该持久化模型。

初始化使用 `CREATE TABLE IF NOT EXISTS` 建立缺失表，兼容仅有记录表的旧数据库。这是当前的初始化兼容方式，尚未建立复杂的版本化数据库迁移框架。

## 计时生命周期

1. **启动**：初始化数据库，读取显示偏好和活动计时，加载已完成记录，刷新统计。
2. **开始**：写入活动计时的主题与当前开始时间，再读取持久化状态更新页面。
3. **显示经过时间**：UI 调度队列的定时器每秒刷新，使用当前 UTC 时间减去活动计时起点；不是把每秒 tick 累加为最终时长。
4. **结束**：在一个 SQLite 事务中读取活动状态、验证时间、插入已完成记录并删除活动计时，然后提交事务。
5. **刷新**：重新加载记录，更新最近记录、筛选记录、累计、趋势和主题聚合。
6. **关闭**：停止 UI 定时器，保留活动计时表。再次启动从原起点恢复经过时间。

结束过程的记录插入与活动状态删除必须共同提交，避免只完成其中一半。活动状态不存在时，结束调用不额外创建记录。UI 命令使用忙状态抑制重复操作，偏好写入使用 `SemaphoreSlim` 串行化。

时长依赖系统时钟，没有单调时钟跨进程校正、空闲识别或休眠排除机制。系统时间回退到开始时间之前时，结束操作报错并保留活动计时。

## 统计计算

### 时间范围与重叠

每个周期使用起点包含、终点排除的时间范围。对一条记录与一个统计桶：

```text
重叠起点 = max(记录开始, 桶开始)
重叠终点 = min(记录结束, 桶结束)
重叠时长 = max(0, UTC(重叠终点) - UTC(重叠起点))
```

桶的边界由当前本地日期确定：日按小时、周按周一到周日、月按自然月中的日、年按自然年中的月、全部按记录涉及的连续月份。正好在桶边界结束的记录不会被重复计入下一桶。

累计时长、趋势图各桶和主题累计采用相同的重叠计算。记录次数按是否存在正时长重叠判断；记录列表的时长则是完整记录时长。具体示例见 [使用手册](user-guide.md#跨午夜记录为什么会分到两天)。

### 主题与页面状态

主题聚合先筛选当前周期内的记录，再去除主题首尾空白并分组，空主题使用「未命名学习」，按周期累计时长降序、名称次序排列。主题下钻使用当前周期和主题名称筛选记录。

保存、删除和周期切换触发统计刷新；UI 定时器检测本地日期变化后也刷新分析。统计只使用已完成记录，不把活动计时作为临时记录加入。

### 图表布局

`StudyChart` 根据实际尺寸、数据量和最高值计算坐标及纵轴量级，通过 Canvas 中的原生图形绘制。折线和柱状消费相同的数据，纵轴按量级显示分钟或小时，数据标记提供提示和辅助功能名称。

趋势点数不超过 31 时适配卡片，标签按空间减少但保留数据和首尾刻度。超过 31 个趋势点或超过 12 个分类点时设置较大最小宽度，由外层 ScrollViewer 提供横向浏览。v1.1.1 将趋势图阈值从 12 改为 31，以覆盖日／月完整范围。

## 视觉与辅助功能

玻璃容器与环境背景形成静态空间层次，按钮的共享选择胶囊、局部渐变和光影由 Windows Composition 驱动。选择形变改变视觉表层，原生 RadioButton 的文字和命中区域维持稳定；键盘和辅助功能操作同步到选择状态。

应用通过系统动画、高级效果和高对比设置降低效果。相关机制不等于全面完成读屏、高对比、输入设备和所有 DPI 环境的测试。更详细的设计范围见 [设计实现](design-implementation.md)。

## 测试与数据隔离

核心检查是独立的 .NET 控制台项目，直接编译数据与统计源文件，使用临时数据库验证行为。它不需要加载 WinUI 窗口。执行方式：

```powershell
dotnet run --project tests/PrivateTimeTrace.Checks/PrivateTimeTrace.Checks.csproj -c Release
```

手动运行开发窗口时，应指定隔离数据文件。例如在项目根目录：

```powershell
$fixturePath = Join-Path $PWD 'artifacts\qa\manual-preview.db'
& '.\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\PrivateTimeTrace.exe' --data-file $fixturePath
```

`--data-file` 接受绝对路径，应用初始化该路径的数据库。已有文件会被正常读取和使用，需确认它属于测试。`--background-test` 仅在指定隔离数据文件时以不主动激活的方式显示测试窗口，供 UI 自动化使用。

| 工具 | 验证目标 |
| --- | --- |
| `tests/PrivateTimeTrace.Checks` | 时间边界、统计总量、主题、SQLite、活动计时和偏好 |
| `tools/verify-ui.ps1` | 原生控件、独立图表选择、偏好恢复、计时流程和窄窗口 |
| `tools/verify-chart-visibility.ps1` | 日／月末端数据标记的可视范围与实际截图像素 |
| `tools/verify-liquid-motion.ps1` | 按钮动效逐帧、局部指针光影、静态内容、快速与键盘选择 |
| `tools/verify-installer.ps1` | 隔离安装、占用保护、覆盖升级、许可、快捷方式和卸载保留 |

测试证据放在 `artifacts/qa/`，不提交个人数据库。图表检查使用专门构造的单条末端记录，确保即使只有 23 点或月末有数据，也能直接看到；不能以按钮已选中代替图形可见性验证。

## 构建与分发

`tools/build-release.ps1` 从项目版本生成 Windows x64 自包含程序，再生成 NSIS 安装器、ZIP 与校验和。依赖还原使用锁定文件；许可收集脚本保留实际包与运行组件的原始条款。

安装目录与数据目录分离，安装器检查运行占用，卸载按文件清单删除程序，保留额外文件和学习数据。GitHub Actions 构建与归档 CI 产物，正式 Release 由维护者核验后发布。

当前记录标识只在本机数据库内有效，数据模型没有跨设备 UUID、同步版本、删除标记或冲突解决机制。如果未来引入同步，需要独立设计数据协议和迁移，不能把复制 SQLite 文件当作现有同步功能。
