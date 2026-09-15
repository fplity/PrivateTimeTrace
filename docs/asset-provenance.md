# 素材说明

## 应用图标

- 源文件：`Assets/AppIcon.png`。
- 用途：应用、任务栏、开始菜单、安装器，以及 README 品牌标识。
- 设计：深蓝玻璃计时环、简洁指针、珍珠银蓝圆角底板与暖白边缘高光。
- 制作方式：2026-09-14 使用内置 imagegen 生成；使用 `tools/build-icon.ps1` 机械缩放、保持透明通道并封装为 Windows ICO，没有复制现有品牌的图标。
- ICO 包含 16、20、24、32、40、48、64、128、256 像素九种尺寸；另生成 Windows 标识资源。
- 原创部分在作者可授予权利的范围内遵循项目 MIT 许可；不保证 AI 生成输出具有排他性或在每个司法管辖区都可取得版权，不构成第三方商标授权。

最终生成提示词：

```text
Use case: logo-brand. Asset type: final Windows desktop application icon, square 1024x1024, for the local study timer app 时间迹 / PrivateTimeTrace. Create ONE production-ready icon, not a presentation or mockup. A softly rounded-square pearl-silver glass tile, straight-on orthographic front view, large and centered occupying 92 percent of the canvas. Inside it, one bold circular timer trace: a deep sapphire-blue translucent glass ring with a subtle small opening near one o'clock, a rounded luminous ice-blue endpoint, and two simple thick clock hands, clear recognizable clock silhouette. The clock symbol should occupy 68 percent of the tile, no tiny ticks or numerals. Refined liquid glass: restrained refraction at the thick beveled edges, silver-blue ambient light, a warm-white upper-left rim highlight, soft internal contact shadow giving real spatial depth, deep enough navy symbol for legibility at 24px. Premium minimalist, sculptural, quiet and polished. Transparent background outside the rounded tile with real alpha; no backdrop rectangle, no checkerboard pattern, no detached ground shadow, no device, no text, no letters, no badges, no watermark, no Apple or other existing brand logo. Symmetrical stable geometry; generous but not excessive safe padding. This is an original app identity, not a copy of any platform's clock icon.
```

## 界面背景与截图

`Assets/LiquidBackdrop.png` 为本项目使用内置 imagegen 生成的环境背景。`docs/design/` 为设计阶段概念参考，不是实际运行证明。

`docs/screenshots/` 的两张图片是本项目原生窗口截图，使用隔离演示数据库，不包含用户真实学习数据。控件、图表、导航、计时与记录列表由 WinUI 原生实现。

系统字体由 Windows 提供，不复制或分发字体文件。内嵌界面符号使用系统字体。安装器使用 NSIS 自带标准向导资源，相关许可见 `licenses/upstream/NSIS-COPYING.txt`。
