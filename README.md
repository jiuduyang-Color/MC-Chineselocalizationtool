# MMCT — Minecraft 模组一键汉化工具

全自动、智能化的 Minecraft 模组汉化命令行工具：扫描 mods 目录，提取英文语言文件，通过 AI 翻译为简体中文，并打包成可直接使用的资源包。

[中文介绍](#中文介绍) · [English](#english)

## 中文介绍

### 功能

- 一键全自动汉化：指定 `.minecraft/versions/<版本>` 文件夹，自动定位 mods 目录、扫描 jar、提取英文条目、AI 翻译、生成资源包
- 独立模组汉化：支持单个 `.jar` 或整目录，可导出 zip 或 json
- 多 AI 引擎：DeepSeek、OpenAI、Claude、Gemini（均通过 OpenAI 兼容接口调用）
- 并发翻译：默认 4 线程（可调 1-16），较单线程提速 3-5 倍
- Token 优化：智能去重 + 紧凑 JSON 载荷，节省 30-60% 费用
- 模组平台验证：Modrinth（免费，无需 Key）+ CurseForge（可选 Key），过滤空壳 / 死库
- 单行进度条 + Minecraft 梗轮播，不刷屏
- 暂停（Space）/ 取消（ESC 带确认）
- 双语界面：`MMCT_ZH.exe`（中文）、`MMCT_EN.exe`（英文），按文件名自动切换
- 配置一览：主菜单选项 6 查看所有配置项（API Key 掩码显示）

### 支持的 AI 服务

均通过 OpenAI 兼容接口调用，在菜单 3（AI 配置）填入 API 地址、Key、模型名即可切换。

| AI 服务 | API 地址 (apiBaseUrl) | 推荐模型 |
| --- | --- | --- |
| DeepSeek（推荐，便宜好用） | `https://api.deepseek.com` | `deepseek-chat` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| Claude (Anthropic) | `https://api.anthropic.com` | `claude-3-5-sonnet-20241022` |
| Gemini (Google) | `https://generativelanguage.googleapis.com` | `gemini-1.5-flash` |
| 其他兼容服务 | 任意 OpenAI 兼容地址 | 对应模型名 |

推荐 DeepSeek：翻译质量好、速度快，费用约为 OpenAI 的 1/10，性价比最高。

### 快速开始

1. 下载 `MMCT_ZH.exe`（中文界面）或 `MMCT_EN.exe`（英文界面）
2. 放到任意目录运行
3. 选择菜单 3（AI 配置），填入你的 AI API Key
4. 选择菜单 1（一键全自动），输入整合包路径
5. 工具自动扫描、翻译、生成资源包到当前目录

### 配置说明

编辑 `config.json` 或通过菜单 5（参数配置）修改：

```json
{
  "apiBaseUrl": "https://api.deepseek.com",
  "apiKey": "你的 API Key",
  "model": "deepseek-chat",
  "concurrency": 4,
  "maxCharsPerBatch": 5000,
  "compactPayload": true,
  "requestTimeoutSeconds": 120,
  "maxRetries": 3,
  "curseForgeApiKey": ""
}
```

| 字段 | 说明 |
| --- | --- |
| apiBaseUrl | AI 服务 API 地址（DeepSeek / OpenAI / Claude / Gemini 见上表） |
| apiKey | AI 服务密钥（DeepSeek 在 platform.deepseek.com 免费申请） |
| model | AI 模型名（见上表推荐） |
| concurrency | 并发线程数 1-16，越大越快但可能被限流 |
| maxCharsPerBatch | 每次请求最大字符数，越大往返越少 |
| compactPayload | Token 优化（去重 + 紧凑 JSON），默认开启 |
| requestTimeoutSeconds | 单次请求超时秒数 |
| maxRetries | 失败重试次数（指数退避） |
| curseForgeApiKey | 可选，填了额外用 CurseForge 验证模组 |

### 支持的 Minecraft 版本

1.16.5 / 1.17.1 / 1.18.2 / 1.19.2 / 1.20.1 / 1.20.4 / 1.21 / 1.21.1 / 1.21.4

### 系统要求

- Windows 10/11 (x64)
- 无需安装 .NET 运行时（单文件自包含发布）

### 技术栈

- C# / .NET 8.0
- 单文件自包含发布（约 34MB）
- 单元测试覆盖核心逻辑

## English

MMCT (Minecraft Mod Chinese Translation Tool) is a Windows command-line tool that automatically scans your Minecraft mods folder, extracts English language files, translates them to Simplified Chinese via AI, and packages everything into a ready-to-use Resource Pack.

### Features

- One-click full auto: point to your `.minecraft/versions/<ver>` folder — the tool finds the mods dir, scans all jars, extracts English entries, translates via AI, and generates a resource pack
- Standalone mod translation: single `.jar` or a folder of mods; export as zip or individual JSON
- Multi-AI engine: DeepSeek, OpenAI, Claude, Gemini (all via OpenAI-compatible API)
- Concurrent translation: 4 threads by default (1-16), 3-5x faster than sequential
- Token optimization: deduplication + compact JSON payload, saves 30-60%
- Mod platform verification: Modrinth (free) + CurseForge (optional key), filters out dead/placeholder mods
- Single-line progress bar with rotating Minecraft puns, no screen spam
- Pause (Space) / Cancel (ESC with confirmation)
- Bilingual UI: `MMCT_ZH.exe` (Chinese) + `MMCT_EN.exe` (English), auto-detected by filename
- Config overview: menu option 6 shows all config values (API keys masked)

### Supported AI Services

All via OpenAI-compatible API. Go to Menu 3 (AI Config) and enter the API URL, Key, and model name to switch.

| AI Service | API URL (apiBaseUrl) | Recommended Model |
| --- | --- | --- |
| DeepSeek (recommended) | `https://api.deepseek.com` | `deepseek-chat` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| Claude (Anthropic) | `https://api.anthropic.com` | `claude-3-5-sonnet-20241022` |
| Gemini (Google) | `https://generativelanguage.googleapis.com` | `gemini-1.5-flash` |
| Other compatible | Any OpenAI-compatible URL | Corresponding model |

DeepSeek recommended: good quality, fast, about 1/10 the cost of OpenAI — best value.

### Quick Start

1. Download `MMCT_ZH.exe` (Chinese UI) or `MMCT_EN.exe` (English UI)
2. Place in any directory and run
3. Choose Menu 3 (AI Config), enter your AI API Key
4. Choose Menu 1 (Full Auto), enter your modpack path
5. The tool scans, translates, and generates a resource pack automatically

### Configuration

Edit `config.json` or use Menu 5 (Parameters):

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

| Field | Description |
| --- | --- |
| apiBaseUrl | AI service API URL (see table above) |
| apiKey | AI service key (free at platform.deepseek.com for DeepSeek) |
| model | AI model name (see recommendations above) |
| concurrency | Concurrent threads 1-16, higher = faster but may hit rate limits |
| maxCharsPerBatch | Max chars per AI request, higher = fewer round trips |
| compactPayload | Token optimization (dedup + compact JSON), on by default |
| requestTimeoutSeconds | Per-request timeout in seconds |
| maxRetries | Retry attempts on failure (exponential backoff) |
| curseForgeApiKey | Optional, enables CurseForge mod verification |

### Supported Minecraft Versions

1.16.5 / 1.17.1 / 1.18.2 / 1.19.2 / 1.20.1 / 1.20.4 / 1.21 / 1.21.1 / 1.21.4

### System Requirements

- Windows 10/11 (x64)
- No .NET runtime needed (self-contained single-file deployment)

### Tech Stack

- C# / .NET 8.0
- Self-contained single-file publish (~34MB)
- Unit tests covering core logic

## License

自定义许可：禁止商用，允许修改 / 二次开发 / 再分发（需注明原作者 jiuduyang），使用风险自负，本项目已停更。详见 [LICENSE](LICENSE)。

Custom license: no commercial use; modification, re-development and redistribution allowed (must credit the original author jiuduyang); use at your own risk; the project is no longer maintained. See [LICENSE](LICENSE).

## Acknowledgments

- [DeepSeek](https://www.deepseek.com/) — AI translation engine
- [Modrinth](https://modrinth.com/) — Mod platform verification
- [CurseForge](https://www.curseforge.com/) — Mod platform verification
- Minecraft is a trademark of Mojang Studios
