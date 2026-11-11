Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "FileFunc.nsh"

!ifndef APP_BUILD
  !define APP_BUILD "1"
!endif
!ifndef ARCH
  !define ARCH "x64"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\artifacts\publish-${ARCH}"
!endif
!ifndef APP_EXE
  !define APP_EXE "HeadphoneHub.exe"
!endif
!ifndef ICON_FILE
  !define ICON_FILE "..\Assets\headphonehub.ico"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "..\artifacts\HeadphoneHub-${ARCH}-Setup.exe"
!endif

!define APP_NAME "HeadphoneHub"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\HeadphoneHub"

Name "${APP_NAME}"
OutFile "${OUT_FILE}"
!if "${ARCH}" == "x86"
  !define REG_VIEW 32
  InstallDir "$PROGRAMFILES32\${APP_NAME}"
!else
  !define REG_VIEW 64
  InstallDir "$PROGRAMFILES64\${APP_NAME}"
!endif
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin
BrandingText "${APP_NAME} build ${APP_BUILD}"

VIProductVersion "1.0.0.${APP_BUILD}"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "LegalCopyright" "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} installer"
VIAddVersionKey "FileVersion" "build ${APP_BUILD}"
VIAddVersionKey "ProductVersion" "build ${APP_BUILD}"

!define MUI_ICON "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE DirLeave
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Start ${APP_NAME}"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function .onInit
!if "${ARCH}" == "x64"
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${APP_NAME} needs 64-bit Windows." /SD IDOK
    Abort
  ${EndIf}
!endif
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "${APP_NAME} needs Windows 10 or newer." /SD IDOK
    Abort
  ${EndIf}
  SetRegView ${REG_VIEW}
  InitPluginsDir
  File "/oname=$PLUGINSDIR\stop-app.ps1" "stop-app.ps1"
FunctionEnd

Function DirLeave
  ${GetFileName} "$INSTDIR" $0
  ${If} $0 != "${APP_NAME}"
    StrCpy $INSTDIR "$INSTDIR\${APP_NAME}"
  ${EndIf}
FunctionEnd

Function LaunchApp
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP_EXE}"'
FunctionEnd

Section "${APP_NAME}" SecApp
  SectionIn RO
  SetShellVarContext all
  SetRegView ${REG_VIEW}

  nsExec::Exec `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\stop-app.ps1" -Dir "$INSTDIR"`
  Pop $0

  SetOutPath "$INSTDIR"
  File /r /x "*.pdb" "${PUBLISH_DIR}\*.*"
  File "stop-app.ps1"
  File "/oname=$INSTDIR\headphonehub.ico" "${ICON_FILE}"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\headphonehub.ico"

  WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayVersion" "build ${APP_BUILD}"
  WriteRegStr HKLM "${UNINST_KEY}" "Publisher" "${APP_NAME}"
  WriteRegStr HKLM "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\headphonehub.ico"
  WriteRegStr HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" $0
SectionEnd

Section /o "Desktop shortcut" SecDesktop
  SetShellVarContext all
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\headphonehub.ico"
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  SetRegView ${REG_VIEW}

  nsExec::Exec `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\stop-app.ps1" -Dir "$INSTDIR"`
  Pop $0

  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  MessageBox MB_YESNO|MB_ICONQUESTION "Also delete your settings (output devices, volumes, theme)?$\r$\n$\r$\n$LOCALAPPDATA\${APP_NAME}" /SD IDNO IDNO keepdata
  SetShellVarContext current
  RMDir /r "$LOCALAPPDATA\${APP_NAME}"
  keepdata:

  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
