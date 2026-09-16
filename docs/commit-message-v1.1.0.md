# v1.1.0 提交信息

## Git commit

```text
feat(release): prepare v1.1.0 button selection motion

- add directional stretch, slight overshoot/rebound and pointer-local highlights inside the selection capsule for style, navigation, period and chart buttons
- keep card backgrounds, wallpaper and page/chart content static; preserve independent trend/topic line-or-bar chart choices and saved preferences
- preserve the local-only data boundary with no cloud sync, account, online learning service or database schema change
- document the Windows x64 self-contained packages, unsigned installer and completed release verification evidence

Verification completed for the final2 packages:
- `verify-installer.ps1 -SkipUi` PASS; this is installer/data verification, not the complete UI regression
- upgrade from 1.0.0 to 1.1.0 completed with installer exit code 0
- formal database unchanged before and after upgrade: `Count=2`; `RecordsHash=7A2975B4D35A343DB0DB6E6834331724F2FCE885AC3EADB854AF9C4F4B6B9D3`; `HasActiveSession=false`; `ActiveHash=74234E98AFE7498FB5DAF1F36AC2D78ACC339464F950703B8C019892F982B90B`; database SHA-256=`9549444129FE41F4E0B3C3D88BD16350AE94DF0B5F673C945A75AF5881805C62`
- install location and shortcut `TargetPath` resolve to `%LOCALAPPDATA%\Programs\PrivateTimeTrace\PrivateTimeTrace.exe`; `IconLocation` resolves to `Assets\AppIcon.ico,0`
- complete UI regression independently passed, including both glass styles, independent chart switching, restart preferences, keyboard/accessibility selection and window resizing
- final2 installer SHA-256=`A1B2C5484CC064EB583DFB29615B063E317484D14B5172811D20D367991EB381`; final2 ZIP SHA-256=`8C84261D95805F2BA2CAA3D4A69FC53461B54879696BEF7373659FAD289227A4`

```

## GitHub 项目简介与主题（已核对）

> 描述：时间迹 — Windows 原生学习计时与可视化。C# / .NET 10 / WinUI 3 / MVVM / SQLite；本地优先、双玻璃风格、独立折线与柱状图切换。

> homepage：https://github.com/fplity/PrivateTimeTrace/releases/latest

主题：`csharp`、`data-visualization`、`dotnet`、`liquid-glass`、`mvvm`、`sqlite`、`study-timer`、`time-tracking`、`windows`、`winui3`。

以上 GitHub About 与主题已和远程仓库核对一致。
