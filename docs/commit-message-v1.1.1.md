# v1.1.1 提交信息

## Git commit

```text
fix(charts): keep day and month data visible within the card

- fit up to 31 trend points inside the chart card so late-day and month-end records remain visible
- thin dense trend labels while preserving every data point and both endpoint labels; keep scrolling for larger histories and topic categories
- add regression coverage for a lone 23:00 14-minute day record and month-end records in 28, 29, 30 and 31 day months
- bump the application and package versions to 1.1.1 / 1.1.1.0
- document the release assets, hashes, installer upgrade evidence and validation boundary

Verification completed:
- 23 core checks PASS
- 16 chart visibility combinations PASS: Day/Month x Line/Bar x Liquid/Frosted x 1380/950
- v1.1.0 baseline reproduces the horizontal-scroll failure
- verify-installer.ps1 -SkipUi PASS
- v1.1.0 to v1.1.1 upgrade completed with installer exit code 0; record count and content fingerprints remained consistent
- installed application DLL SHA-256 matches the tested package
```

## GitHub About

描述：时间迹 — Windows 原生本地学习计时与可视化。C# / .NET 10 / WinUI 3 / MVVM / SQLite；本地优先、双玻璃风格、独立折线与柱状图切换。

homepage：https://github.com/fplity/PrivateTimeTrace/releases/latest

主题保持 10 项：`csharp`、`data-visualization`、`dotnet`、`liquid-glass`、`mvvm`、`sqlite`、`study-timer`、`time-tracking`、`windows`、`winui3`。
