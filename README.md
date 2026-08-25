# MMCT - Minecraft 模组一键汉化工具

<div align="center">

**全自动、智能化的 Minecraft 模组汉化解决方案**

[中文](#中文介绍) | [English](#english-introduction)

</div>

---

## 中文介绍

### 这是什么？

MMCT（Minecraft Mod Chinese Translation Tool）是一个 Windows 命令行工具，能够自动扫描你的 Minecraft mods 文件夹，提取所有模组的英文语言文件（`en_us.json`），通过 AI 翻译引擎将它们翻译成简体中文（`zh_cn.json`），并打包成可直接使用的资源包（Resource Pack）。

### 核心功能

| 功能 | 说明 |
|---|---|
| **一键全自动汉化** | 指定 `.minecraft/versions/<版本>` 文件夹，工具自动定位 mods 目录、扫描所有 jar、提取英文条目、AI 翻译、生成资源包 |
| **独立模组汉化** | 支持单个 `.jar` 文件或整个目录的汉化，翻译完成后可选择导出 zip 或 json |
| **多 AI 引擎支持** | DeepSeek、OpenAI (GPT-3.5/4)、Claude、Gemini，通过 OpenAI 兼容接口统一调用 |
| **并发翻译加速** | 默认 4 线程并发翻译多个模组（可调 1-16），相比单线程提速 3-5 倍 |
| **Token 优化** | 智能去重（相同英文只翻译一次）+ 紧凑 JSON 载荷，节省 30-60% token 消耗 |
| **模组平台验证** | 自动连接 Modrinth（免费，无需 Key）和 CurseForge（可选 API Key）验证模组是否真实存在，过滤空壳/死库 |
| **单行进度条** | 不刷屏，一行动态刷新进度 + 旋转的 Minecraft 梗（"It's not a bug, it's a feature!"） |
| **暂停/取消** | 翻译中按 `Space` 暂停/继续，按 `ESC` 弹出取消确认 |
| **双语界面** | 中文版 `MMCT_ZH.exe` + 英文版 `MMCT_EN.exe`，根据文件名自动切换语言 |
| **配置一览** | 主菜单选项 6 可查看当前所有配置项的值（API Key 掩码显示） |

### 快速开始

1. 下载 `MMCT_ZH.exe`（中文界面）或 `MMCT_EN.exe`（英文界面）
2. 放到任意目录，运行
3. 选择 **菜单 3（AI 配置）**，填入你的 DeepSeek API Key（[免费申请](https://platform.deepseek.com/)）
4. 选择 **菜单 1（一键全自动）**，输入你的 `.minecraft/versions/1.21.1` 路径
5. 工具自动扫描、翻译、生成资源包到当前目录

### 配置说明

打开 `config.json` 或通过 **菜单 5（参数配置）** 修改：

```json
{
  "apiBaseUrl": "https://api.deepseek.com",
  "apiKey": "你的API Key",
  "model": "deepseek-chat",
  "concurrency": 4,           // 并发线程数 (1-16)
  "maxCharsPerBatch": 5000,   // 每批最大字符数
  "compactPayload": true,    // Token 优化（去重+紧凑JSON）
  "requestTimeoutSeconds": 120,
  "maxRetries": 3,
  "curseForgeApiKey": ""      // 可选，填了会查 CurseForge
}
```

### 支持的 Minecraft 版本

1.16.5 / 1.17.1 / 1.18.2 / 1.19.2 / 1.20.1 / 1.20.4 / 1.21 / 1.21.1 / 1.21.4

### 系统要求

- Windows 10/11 (x64)
- 无需安装 .NET 运行时（单文件自包含发布）

### 技术栈

- C# / .NET 8.0
- 单文件自包含发布（~34MB）
- 71 个单元测试全覆盖

---

## English Introduction

### What is this?

MMCT (Minecraft Mod Chinese Translation Tool) is a Windows command-line tool that automatically scans your Minecraft mods folder, extracts all English language files (`en_us.json`) from mod jars, translates them to Simplified Chinese (`zh_cn.json`) via AI, and packages everything into a ready-to-use Resource Pack.

### Key Features

| Feature | Description |
|---|---|
| **One-Click Full Auto** | Point to your `.minecraft/versions/<ver>` folder — the tool finds the mods dir, scans all jars, extracts English entries, translates via AI, and generates a resource pack |
| **Standalone Mod Translation** | Translate a single `.jar` or a folder of mods; export as zip or individual JSON files |
| **Multi-AI Engine** | DeepSeek, OpenAI (GPT-3.5/4), Claude, Gemini — all via OpenAI-compatible API |
| **Concurrent Translation** | 4 threads by default (adjustable 1-16), 3-5x faster than sequential |
| **Token Optimization** | Deduplication (identical English texts translated once) + compact JSON payload saves 30-60% tokens |
| **Mod Platform Verification** | Checks Modrinth (free, no key) and CurseForge (optional API key) to filter out dead/placeholder mods |
| **Single-Line Progress Bar** | No screen spam — one dynamically refreshing line with rotating Minecraft puns |
| **Pause / Cancel** | Press `Space` to pause/resume, `ESC` for cancel confirmation during translation |
| **Bilingual UI** | `MMCT_ZH.exe` (Chinese) + `MMCT_EN.exe` (English) — auto-detected by filename |
| **Config Overview** | Menu option 6 shows all current config values (API keys masked) |

### Quick Start

1. Download `MMCT_ZH.exe` (Chinese UI) or `MMCT_EN.exe` (English UI)
2. Place in any directory and run
3. Choose **Menu 3 (AI Config)**, enter your DeepSeek API Key ([get free](https://platform.deepseek.com/))
4. Choose **Menu 1 (Full Auto)**, enter your `.minecraft/versions/1.21.1` path
5. The tool scans, translates, and generates a resource pack automatically

### Configuration

Edit `config.json` or use **Menu 5 (Parameters)**:

```json
{
  "apiBaseUrl": "https://api.deepseek.com",
  "apiKey": "your-api-key",
  "model": "deepseek-chat",
  "concurrency": 4,
  "maxCharsPerBatch": 5000,
  "compactPayload": true,
  "requestTimeoutSeconds": 120,
  "maxRetries": 3,
  "curseForgeApiKey": ""
}
```

### Supported Minecraft Versions

1.16.5 / 1.17.1 / 1.18.2 / 1.19.2 / 1.20.1 / 1.20.4 / 1.21 / 1.21.1 / 1.21.4

### System Requirements

- Windows 10/11 (x64)
- No .NET runtime needed (self-contained single-file deployment)

### Tech Stack

- C# / .NET 8.0
- Self-contained single-file publish (~34MB)
- 71 unit tests covering all core logic

---

## License

MIT

## Acknowledgments

- [DeepSeek](https://www.deepseek.com/) — AI translation engine
- [Modrinth](https://modrinth.com/) — Mod platform verification
- [CurseForge](https://www.curseforge.com/) — Mod platform verification
- Minecraft is a trademark of Mojang Studios
