# 反重力汉化开关 (Antigravity CN Switch)

基于 [Anarcadia/chuanban-antigravity-theme-patch](https://github.com/Anarcadia/chuanban-antigravity-theme-patch) (`v5.4.2`) 二次创作的极简桌面开关 App（支持 **macOS** 与 **Windows**）。

## 功能特点

- **一键开关「开启汉化」**：
  - **开启**：自动部署补丁、配置调试端口并启动后台守护服务（内置独立 Node.js v22.14.0 运行时，无需本机额外安装 Node）。之后打开 Antigravity（或已打开的窗口）即刻变为汉化 + 暖纸护眼主题 + 实时 `tok/s` / `TTFT` / 上下文水位显示。
  - **关闭**：自动停止守护服务并卸载注入，Antigravity 立即恢复原版英文界面。
- **主题偏好切换**：支持白天主题（高对比暖白 / Claude 净白 / 复古羊皮纸）与夜间主题（官方碳黑 / Claude 暖纸深夜）即时切换。

## 本地构建

### macOS (`arm64` / `x64`)
```bash
chmod +x mac/build_mac.sh
./mac/build_mac.sh
# 产物位于 dist/反重力汉化开关.app
```

### Windows (`x64`)
在 Windows PowerShell 中直接运行（使用系统自带 `.NET Framework 4.x csc.exe` 编译，零额外编译工具依赖）：
```powershell
.\windows\build_win.ps1
# 产物位于 dist\AntigravityCnSwitch-Windows\反重力汉化开关.exe
```

### 使用 GitHub Actions 虚拟机自动打包 Windows / macOS
`github-workflow/build.yml` 已配置好 `windows-latest` 与 `macos-latest` 自动构建工作流。将其放到 `.github/workflows/build.yml` 并推送至 `main` 分支，即可在 GitHub Actions 页面下载打包好的 `AntigravityCnSwitch-Windows-x64.zip` 与 `AntigravityCnSwitch-macOS-arm64.zip`。
