import SwiftUI
import AppKit
import Foundation

// MARK: - Models & Patch Controller

enum DayPreset: String, CaseIterable, Identifiable {
    case highContrast = "high-contrast"
    case claudeWhite = "claude-desktop"
    case vintageParchment = "parchment"

    var id: String { rawValue }
    var label: String {
        switch self {
        case .highContrast: return "高对比暖白"
        case .claudeWhite: return "Claude 净白"
        case .vintageParchment: return "复古羊皮纸"
        }
    }
}

enum NightPreset: String, CaseIterable, Identifiable {
    case onyx = "onyx"
    case claudeWarm = "claude-warm"

    var id: String { rawValue }
    var label: String {
        switch self {
        case .onyx: return "官方碳黑"
        case .claudeWarm: return "Claude 暖纸深夜"
        }
    }
}

final class PatchController: ObservableObject {
    @Published var isEnabled: Bool = false
    @Published var isBusy: Bool = false
    @Published var statusMessage: String = "正在检测状态..."
    @Published var antigravityRunning: Bool = false
    @Published var hasFonts: Bool = false
    @Published var dayPreset: DayPreset = .highContrast
    @Published var nightPreset: NightPreset = .onyx
    @Published var errorText: String? = nil

    private let fm = FileManager.default
    private var timer: Timer?

    private var homeDir: String { fm.homeDirectoryForCurrentUser.path }
    private var targetDir: String { "\(homeDir)/.gemini/antigravity/theme-patch" }
    private var userConfigPath: String { "\(targetDir)/user/config.json" }
    private var runtimeNodePath: String { "\(targetDir)/runtime/node" }
    private var plistPath: String { "\(homeDir)/Library/LaunchAgents/com.antigravity.theme.plist" }

    private var bundledResourcesDir: String {
        Bundle.main.resourcePath ?? Bundle.main.bundlePath
    }
    private var bundledPatchDir: String { "\(bundledResourcesDir)/patch" }
    private var bundledNodePath: String { "\(bundledResourcesDir)/runtime/node" }

    init() {
        refreshState()
        timer = Timer.scheduledTimer(withTimeInterval: 2.5, repeats: true) { [weak self] _ in
            self?.checkEnvironmentOnly()
        }
    }

    deinit {
        timer?.invalidate()
    }

    func refreshState() {
        checkEnvironmentOnly()
        let config = readUserConfig()
        let cfgEnabled = (config["enabled"] as? Bool) ?? false
        if let p = config["preset"] as? String, let dp = DayPreset(rawValue: p) {
            dayPreset = dp
        }
        if let dp = config["darkPreset"] as? String, let np = NightPreset(rawValue: dp) {
            nightPreset = np
        }

        let serviceActive = isServiceLoaded()
        isEnabled = cfgEnabled && serviceActive
        statusMessage = isEnabled
            ? "已开启 · Antigravity 将自动显示中文界面与暖纸护眼主题"
            : "已关闭 · Antigravity 保持原版英文界面"
    }

    private func checkEnvironmentOnly() {
        let running = NSWorkspace.shared.runningApplications.contains { app in
            let name = app.localizedName ?? ""
            let bid = app.bundleIdentifier ?? ""
            return name.localizedCaseInsensitiveContains("Antigravity") && !bid.contains("antigravity-cn-switch")
        }
        antigravityRunning = running

        let fontDirs = ["\(homeDir)/Library/Fonts", "/Library/Fonts"]
        var found = false
        for dir in fontDirs {
            if let files = try? fm.contentsOfDirectory(atPath: dir) {
                if files.contains(where: { $0.localizedCaseInsensitiveContains("Sarasa") || $0.localizedCaseInsensitiveContains("Anthropic") || $0.contains("更纱") }) {
                    found = true
                    break
                }
            }
        }
        hasFonts = found
    }

    func toggleEnabled(_ newValue: Bool) {
        guard !isBusy else { return }
        isBusy = true
        errorText = nil
        statusMessage = newValue ? "正在开启汉化并启动守护服务..." : "正在关闭汉化并恢复英文原貌..."

        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self = self else { return }
            do {
                let nodeBin = try self.ensureRuntimeNode()
                let installScript = self.resolveInstallScript()

                if newValue {
                    self.updateUserConfig { cfg in
                        cfg["enabled"] = true
                        cfg["preset"] = self.dayPreset.rawValue
                        cfg["darkPreset"] = self.nightPreset.rawValue
                    }
                    let res = self.runCommand(nodeBin, args: ["--disable-warning=ExperimentalWarning", installScript, "install"])
                    if res.exitCode != 0 {
                        throw NSError(domain: "Patch", code: Int(res.exitCode), userInfo: [NSLocalizedDescriptionKey: res.output.isEmpty ? "安装失败 (exit \(res.exitCode))" : res.output])
                    }
                } else {
                    if self.fm.fileExists(atPath: installScript) {
                        let res = self.runCommand(nodeBin, args: ["--disable-warning=ExperimentalWarning", installScript, "uninstall"])
                        if res.exitCode != 0 {
                            throw NSError(domain: "Patch", code: Int(res.exitCode), userInfo: [NSLocalizedDescriptionKey: res.output.isEmpty ? "卸载失败 (exit \(res.exitCode))" : res.output])
                        }
                    }
                    self.updateUserConfig { cfg in
                        cfg["enabled"] = false
                    }
                }

                DispatchQueue.main.async {
                    self.isBusy = false
                    self.refreshState()
                }
            } catch {
                DispatchQueue.main.async {
                    self.isBusy = false
                    self.errorText = error.localizedDescription
                    self.refreshState()
                }
            }
        }
    }

    func applyPresets(day: DayPreset, night: NightPreset) {
        dayPreset = day
        nightPreset = night
        updateUserConfig { cfg in
            cfg["preset"] = day.rawValue
            cfg["darkPreset"] = night.rawValue
        }
    }

    // MARK: - Helpers

    private func resolveInstallScript() -> String {
        let bundledSrc = "\(bundledPatchDir)"
        let localBundled = "\(targetDir)/bundled-patch"
        if fm.fileExists(atPath: "\(bundledSrc)/install.mjs") {
            let srcManifest = "\(bundledSrc)/core/manifest.json"
            let dstManifest = "\(localBundled)/core/manifest.json"
            let needCopy = !fm.fileExists(atPath: "\(localBundled)/install.mjs")
                || (fm.contents(atPath: srcManifest) != fm.contents(atPath: dstManifest))
            if needCopy {
                try? fm.removeItem(atPath: localBundled)
                try? fm.createDirectory(atPath: targetDir, withIntermediateDirectories: true)
                try? fm.copyItem(atPath: bundledSrc, toPath: localBundled)
            }
            if fm.fileExists(atPath: "\(localBundled)/install.mjs") {
                return "\(localBundled)/install.mjs"
            }
        }
        return "\(targetDir)/install.mjs"
    }

    private func ensureRuntimeNode() throws -> String {
        let runtimeDir = "\(targetDir)/runtime"
        try fm.createDirectory(atPath: runtimeDir, withIntermediateDirectories: true)

        if fm.fileExists(atPath: bundledNodePath) {
            if !fm.fileExists(atPath: runtimeNodePath) {
                try fm.copyItem(atPath: bundledNodePath, toPath: runtimeNodePath)
                try fm.setAttributes([.posixPermissions: 0o755], ofItemAtPath: runtimeNodePath)
            }
            return runtimeNodePath
        }

        if fm.fileExists(atPath: runtimeNodePath) {
            return runtimeNodePath
        }

        for candidate in ["/opt/homebrew/bin/node", "/usr/local/bin/node"] {
            if fm.isExecutableFile(atPath: candidate) {
                return candidate
            }
        }
        throw NSError(domain: "Patch", code: 1, userInfo: [NSLocalizedDescriptionKey: "未找到 Node.js 22+ 运行时"])
    }

    private func isServiceLoaded() -> Bool {
        if !fm.fileExists(atPath: plistPath) { return false }
        let uid = getuid()
        let res = runCommand("/bin/launchctl", args: ["print", "gui/\(uid)/com.antigravity.theme"])
        return res.exitCode == 0
    }

    private func readUserConfig() -> [String: Any] {
        guard let data = fm.contents(atPath: userConfigPath),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return [:]
        }
        return obj
    }

    private func updateUserConfig(_ mutate: (inout [String: Any]) -> Void) {
        let userDir = "\(targetDir)/user"
        try? fm.createDirectory(atPath: userDir, withIntermediateDirectories: true)
        var cfg = readUserConfig()
        if cfg.isEmpty {
            let templatePath = "\(bundledPatchDir)/user/config.json"
            if let data = fm.contents(atPath: templatePath),
               let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                cfg = obj
            }
        }
        mutate(&cfg)
        if let data = try? JSONSerialization.data(withJSONObject: cfg, options: [.prettyPrinted, .sortedKeys]) {
            try? data.write(to: URL(fileURLWithPath: userConfigPath))
        }
    }

    private func runCommand(_ launchPath: String, args: [String]) -> (exitCode: Int32, output: String) {
        let proc = Process()
        proc.executableURL = URL(fileURLWithPath: launchPath)
        proc.arguments = args
        var env = ProcessInfo.processInfo.environment
        env["PATH"] = "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin"
        proc.environment = env
        let pipe = Pipe()
        proc.standardOutput = pipe
        proc.standardError = pipe
        do {
            try proc.run()
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            proc.waitUntilExit()
            let out = (String(data: data, encoding: .utf8) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            if proc.terminationStatus != 0 {
                try? "[\(Date())] \(launchPath) \(args.joined(separator: " ")) -> exit \(proc.terminationStatus)\n\(out)\n".write(
                    toFile: "/tmp/antigravity-cn-switch.log",
                    atomically: true,
                    encoding: .utf8
                )
            }
            return (proc.terminationStatus, out)
        } catch {
            return (-1, error.localizedDescription)
        }
    }
}

// MARK: - SwiftUI View

struct ContentView: View {
    @StateObject private var controller = PatchController()

    private let warmPaper = Color(red: 0.969, green: 0.961, blue: 0.941) // #F7F5F0
    private let cardBg = Color(red: 0.988, green: 0.988, blue: 0.984)    // #FCFCFB
    private let waxRed = Color(red: 0.478, green: 0.122, blue: 0.102)    // #7A1F1A
    private let warmOrange = Color(red: 0.851, green: 0.467, blue: 0.341) // #D97757
    private let inkColor = Color(red: 0.08, green: 0.08, blue: 0.08)

    var body: some View {
        VStack(spacing: 18) {
            // Header
            VStack(spacing: 6) {
                HStack(spacing: 8) {
                    Image(systemName: "character.book.closed.fill")
                        .font(.system(size: 22))
                        .foregroundColor(waxRed)
                    Text("反重力汉化开关")
                        .font(.system(size: 20, weight: .bold))
                        .foregroundColor(inkColor)
                }
                Text("船板全栈补丁 v5.4.2 · 汉化 / 暖纸护眼 / 实时指标")
                    .font(.system(size: 11.5))
                    .foregroundColor(.secondary)
            }
            .padding(.top, 6)

            // Main Toggle Card
            VStack(spacing: 14) {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("开启汉化")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(inkColor)
                        Text(controller.isEnabled ? "已开启 · 中文界面 + 暖纸主题" : "已关闭 · 原版英文界面")
                            .font(.system(size: 12))
                            .foregroundColor(controller.isEnabled ? waxRed : .secondary)
                    }
                    Spacer()
                    if controller.isBusy {
                        ProgressView()
                            .scaleEffect(0.8)
                            .frame(width: 48, height: 26)
                    } else {
                        Toggle("", isOn: Binding(
                            get: { controller.isEnabled },
                            set: { controller.toggleEnabled($0) }
                        ))
                        .toggleStyle(SwitchToggleStyle(tint: waxRed))
                        .labelsHidden()
                        .scaleEffect(1.15)
                    }
                }

                Divider()

                Text(controller.statusMessage)
                    .font(.system(size: 11.5))
                    .foregroundColor(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(16)
            .background(cardBg)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(controller.isEnabled ? waxRed.opacity(0.45) : Color.gray.opacity(0.2), lineWidth: 1.5)
            )

            // Theme Presets Card
            VStack(spacing: 10) {
                HStack {
                    Text("白天主题")
                        .font(.system(size: 12.5, weight: .medium))
                        .foregroundColor(inkColor)
                    Spacer()
                    Picker("", selection: Binding(
                        get: { controller.dayPreset },
                        set: { controller.applyPresets(day: $0, night: controller.nightPreset) }
                    )) {
                        ForEach(DayPreset.allCases) { p in
                            Text(p.label).tag(p)
                        }
                    }
                    .labelsHidden()
                    .frame(width: 150)
                }

                HStack {
                    Text("夜间主题")
                        .font(.system(size: 12.5, weight: .medium))
                        .foregroundColor(inkColor)
                    Spacer()
                    Picker("", selection: Binding(
                        get: { controller.nightPreset },
                        set: { controller.applyPresets(day: controller.dayPreset, night: $0) }
                    )) {
                        ForEach(NightPreset.allCases) { p in
                            Text(p.label).tag(p)
                        }
                    }
                    .labelsHidden()
                    .frame(width: 150)
                }
            }
            .padding(14)
            .background(cardBg)
            .cornerRadius(10)
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .stroke(Color.gray.opacity(0.18), lineWidth: 1)
            )

            if let err = controller.errorText {
                Text(err)
                    .font(.system(size: 11))
                    .foregroundColor(.red)
                    .lineLimit(3)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }

            Spacer(minLength: 0)

            // Footer Status
            HStack(spacing: 10) {
                HStack(spacing: 5) {
                    Circle()
                        .fill(controller.antigravityRunning ? Color.green : Color.gray.opacity(0.5))
                        .frame(width: 7, height: 7)
                    Text(controller.antigravityRunning ? "Antigravity 运行中" : "Antigravity 未运行")
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                }
                Spacer()
                if !controller.hasFonts {
                    Link("下载推荐字体", destination: URL(string: "https://drive.google.com/drive/folders/1NX92SgVUqtSjWg6bt6sIJXy-cn7J_boC")!)
                        .font(.system(size: 11))
                        .foregroundColor(warmOrange)
                } else {
                    Text("字体已就绪")
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding(20)
        .frame(width: 360, height: 390)
        .background(warmPaper)
    }
}

// MARK: - App Entry

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return true
    }
}

@main
struct AntigravityCnSwitchApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate

    var body: some Scene {
        WindowGroup("反重力汉化开关") {
            ContentView()
        }
        .windowResizability(.contentSize)
    }
}
