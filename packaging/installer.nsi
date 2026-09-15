Unicode true
ManifestDPIAware true
RequestExecutionLevel user
SetCompressor /SOLID zlib
SetDatablockOptimize on
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WinVer.nsh"
!include "x64.nsh"

!define APP_ID "PrivateTimeTrace"
!define OWNER_ID "842fda49-799d-47a5-a9e4-38a7d30bfe39"
Name "时间迹 ${VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\PrivateTimeTrace"
BrandingText "时间迹 · 专注此刻，留下痕迹"
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "时间迹"
VIAddVersionKey /LANG=2052 "CompanyName" "fplity"
VIAddVersionKey /LANG=2052 "LegalCopyright" "Copyright (c) 2026 fplity"
VIAddVersionKey /LANG=2052 "FileDescription" "时间迹 Windows x64 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${VERSION}"
!define MUI_ICON "${APP_DIR}\Assets\AppIcon.ico"
!define MUI_UNICON "${APP_DIR}\Assets\AppIcon.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "欢迎安装时间迹"
!define MUI_WELCOMEPAGE_TEXT "记录学习时间，观察专注的变化。$\r$\n$\r$\n安装仅影响当前 Windows 用户，无需管理员权限。$\r$\n学习数据保存在独立的本地目录，升级与卸载都会保留。$\r$\n$\r$\n升级前请先关闭已安装的时间迹窗口。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_LICENSEPAGE_TEXT_TOP "请阅读应用 MIT 许可及附带运行库条款。第三方组件不受本项目 MIT 重新授权。"
!insertmacro MUI_PAGE_LICENSE "${TERMS_FILE}"
!define MUI_LICENSEPAGE_TEXT_TOP "以下为附带 Windows SDK 组件的微软原始许可。安装即表示同意适用的运行库条款。"
!insertmacro MUI_PAGE_LICENSE "${APP_DIR}\licenses\upstream\windows-sdk-license.rtf"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\PrivateTimeTrace.exe"
!define MUI_FINISHPAGE_RUN_TEXT "打开时间迹"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Var RegKey
Var EntryName
Var InstallMode
Var Parameters
Var OptionValue
Var SetupMutex

Function .onInit
    SetShellVarContext current
    SetRegView 64
    StrCpy $LANGUAGE 2052
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "此安装包需要 64 位 Windows。" /SD IDOK
        SetErrorLevel 2
        Abort
    ${EndIf}
    ${IfNot} ${AtLeastBuild} 17763
        MessageBox MB_OK|MB_ICONSTOP "需要 Windows 10 1809 或更高版本，推荐 Windows 11。" /SD IDOK
        SetErrorLevel 2
        Abort
    ${EndIf}
    ${GetParameters} $Parameters
    IfSilent 0 licenses_checked
    ClearErrors
    ${GetOptions} $Parameters "/ACCEPTLICENSES" $OptionValue
    ${If} ${Errors}
        SetErrorLevel 3
        Abort
    ${EndIf}
    licenses_checked:
    StrCpy $INSTDIR "$LOCALAPPDATA\Programs\PrivateTimeTrace"
    StrCpy $RegKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\PrivateTimeTrace"
    StrCpy $EntryName "时间迹"
    StrCpy $InstallMode "normal"
    ClearErrors
    ${GetOptions} $Parameters "/TESTINSTALL" $OptionValue
    ${IfNot} ${Errors}
        StrCpy $INSTDIR "$LOCALAPPDATA\Programs\PrivateTimeTrace.InstallTest"
        StrCpy $RegKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\PrivateTimeTrace.InstallTest"
        StrCpy $EntryName "时间迹（安装验证）"
        StrCpy $InstallMode "test"
    ${EndIf}
    System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\PrivateTimeTrace.Setup") p .r0 ?e'
    Pop $1
    StrCpy $SetupMutex $0
    ${If} $1 = 183
        MessageBox MB_OK|MB_ICONSTOP "另一个时间迹安装程序正在运行。" /SD IDOK
        SetErrorLevel 4
        Abort
    ${EndIf}
    IfFileExists "$INSTDIR\.timetrace-install.ini" check_owner check_empty
    check_owner:
        ReadINIStr $0 "$INSTDIR\.timetrace-install.ini" "Install" "Owner"
        ${If} $0 != "${OWNER_ID}"
            Goto invalid_directory
        ${EndIf}
        Goto check_running
    check_empty:
        FindFirst $0 $1 "$INSTDIR\*"
        check_next:
        ${If} $1 == ""
            FindClose $0
            Goto check_running
        ${EndIf}
        ${If} $1 != "."
        ${AndIf} $1 != ".."
            FindClose $0
            Goto invalid_directory
        ${EndIf}
        FindNext $0 $1
        Goto check_next
    invalid_directory:
        MessageBox MB_OK|MB_ICONSTOP "目标目录包含不属于本安装程序的文件。为保护数据，已停止安装：$\r$\n$INSTDIR" /SD IDOK
        SetErrorLevel 5
        Abort
    check_running:
        IfFileExists "$INSTDIR\PrivateTimeTrace.exe" 0 ready
        System::Call 'kernel32::CreateFileW(w "$INSTDIR\PrivateTimeTrace.exe", i 0x40000000, i 0, p 0, i 3, i 0, p 0) p .r0'
        ${If} $0 == -1
            MessageBox MB_OK|MB_ICONSTOP "请先关闭已安装的时间迹窗口，再重新运行安装程序。" /SD IDOK
            SetErrorLevel 6
            Abort
        ${EndIf}
        System::Call 'kernel32::CloseHandle(p r0)'
    ready:
FunctionEnd

Function .onGUIEnd
    ${If} $SetupMutex != ""
        System::Call 'kernel32::CloseHandle(p $SetupMutex)'
    ${EndIf}
FunctionEnd

Section "时间迹（必需）" MainSection
    SectionIn RO
    SetOutPath "$INSTDIR"
    WriteINIStr "$INSTDIR\.timetrace-install.ini" "Install" "Owner" "${OWNER_ID}"
    WriteINIStr "$INSTDIR\.timetrace-install.ini" "Install" "Mode" "$InstallMode"
    SetOverwrite on
    !include "${INSTALL_INCLUDE}"
    SetOutPath "$INSTDIR"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    CreateDirectory "$SMPROGRAMS\$EntryName"
    CreateShortcut "$SMPROGRAMS\$EntryName\$EntryName.lnk" "$INSTDIR\PrivateTimeTrace.exe" "" "$INSTDIR\Assets\AppIcon.ico"
    CreateShortcut "$SMPROGRAMS\$EntryName\卸载时间迹.lnk" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKCU "$RegKey" "DisplayName" "$EntryName"
    WriteRegStr HKCU "$RegKey" "DisplayVersion" "${VERSION}"
    WriteRegStr HKCU "$RegKey" "Publisher" "fplity"
    WriteRegStr HKCU "$RegKey" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "$RegKey" "DisplayIcon" "$INSTDIR\Assets\AppIcon.ico"
    WriteRegStr HKCU "$RegKey" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "$RegKey" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegStr HKCU "$RegKey" "URLInfoAbout" "https://github.com/fplity/PrivateTimeTrace"
    WriteRegDWORD HKCU "$RegKey" "EstimatedSize" ${SIZE_KB}
    WriteRegDWORD HKCU "$RegKey" "NoModify" 1
    WriteRegDWORD HKCU "$RegKey" "NoRepair" 1
    SetErrorLevel 0
SectionEnd

Section /o "桌面快捷方式" DesktopSection
    CreateShortcut "$DESKTOP\$EntryName.lnk" "$INSTDIR\PrivateTimeTrace.exe" "" "$INSTDIR\Assets\AppIcon.ico"
SectionEnd

Function un.onInit
    SetShellVarContext current
    SetRegView 64
    StrCpy $LANGUAGE 2052
    ReadINIStr $0 "$INSTDIR\.timetrace-install.ini" "Install" "Owner"
    ReadINIStr $InstallMode "$INSTDIR\.timetrace-install.ini" "Install" "Mode"
    ${If} $0 != "${OWNER_ID}"
        MessageBox MB_OK|MB_ICONSTOP "缺少有效安装标记，已停止卸载。" /SD IDOK
        SetErrorLevel 5
        Abort
    ${EndIf}
    StrCpy $RegKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\PrivateTimeTrace"
    StrCpy $EntryName "时间迹"
    StrCpy $0 "$LOCALAPPDATA\Programs\PrivateTimeTrace"
    ${If} $InstallMode == "test"
        StrCpy $RegKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\PrivateTimeTrace.InstallTest"
        StrCpy $EntryName "时间迹（安装验证）"
        StrCpy $0 "$LOCALAPPDATA\Programs\PrivateTimeTrace.InstallTest"
    ${EndIf}
    ${If} $INSTDIR != $0
        MessageBox MB_OK|MB_ICONSTOP "卸载目录与已知专用目录不一致，已停止。" /SD IDOK
        SetErrorLevel 5
        Abort
    ${EndIf}
    IfFileExists "$INSTDIR\PrivateTimeTrace.exe" 0 uninstall_ready
    System::Call 'kernel32::CreateFileW(w "$INSTDIR\PrivateTimeTrace.exe", i 0x40000000, i 0, p 0, i 3, i 0, p 0) p .r0'
    ${If} $0 == -1
        MessageBox MB_OK|MB_ICONSTOP "请先关闭时间迹，再进行卸载。" /SD IDOK
        SetErrorLevel 6
        Abort
    ${EndIf}
    System::Call 'kernel32::CloseHandle(p r0)'
    uninstall_ready:
FunctionEnd

Section "Uninstall"
    # Delete only the exact shipped files; never recursively remove a directory.
    !include "${UNINSTALL_INCLUDE}"
    Delete "$SMPROGRAMS\$EntryName\$EntryName.lnk"
    Delete "$SMPROGRAMS\$EntryName\卸载时间迹.lnk"
    RMDir "$SMPROGRAMS\$EntryName"
    Delete "$DESKTOP\$EntryName.lnk"
    DeleteRegKey HKCU "$RegKey"
    Delete "$INSTDIR\.timetrace-install.ini"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
    SetErrorLevel 0
SectionEnd
