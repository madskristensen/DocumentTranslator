# Document Translator

[![Build](https://github.com/madskristensen/DocumentTranslator/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/DocumentTranslator/actions/workflows/build.yaml)
[![License](https://img.shields.io/badge/license-See%20LICENSE.txt-blue)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)](#)

A lightweight Windows desktop app for translating whole documents into one or more languages using **Azure AI Translator**.

Drag a file in, pick your languages, and get translated copies side-by-side with the original — formatting preserved.

---

## ✨ Features

- 🌍 **Translate to many languages at once** — pick one or more targets in a single run.
- 📄 **Multiple formats supported** — Word (`.docx`), PDF, HTML, Markdown, and plain text.
- 🪄 **Drag-and-drop** — just drop a document onto the window.
- 🎯 **Smart source detection** — choose a source language or let Azure auto-detect.
- 🧠 **Powered by Azure AI Translator** — high-quality, document-level translation that preserves layout.
- 🖥️ **Native WPF UI** — fast, responsive, and at home on Windows.

## 📦 Supported file types

| Type     | Extensions          |
| -------- | ------------------- |
| Word     | `.docx`             |
| PDF      | `.pdf`              |
| HTML     | `.html`, `.htm`     |
| Markdown | `.md`, `.markdown`  |
| Text     | `.txt`              |

## 🚀 Getting started

### Prerequisites

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [Azure AI Translator](https://learn.microsoft.com/azure/ai-services/translator/) resource (key + endpoint)

### Build & run

```powershell
git clone https://github.com/madskristensen/DocumentTranslator.git
cd DocumentTranslator
dotnet run --project DocumentTranslator
```

Or open `DocumentTranslator.slnx` in Visual Studio 2026 and press <kbd>F5</kbd>.

### Configure Azure credentials

1. Launch the app.
2. Click the ⚙️ **Settings** button in the top-right.
3. Enter your **Translator endpoint** and **key**.
4. Save — you're ready to translate.

## 🧭 How to use

1. **Browse** for a document, or **drag-and-drop** one onto the window.
2. Pick the **source language** (or leave on Auto-detect).
3. Select one or more **target languages**.
4. Click **Translate**.

Translated files are written next to the source, suffixed with the target language code.

## 🛠️ Tech stack

- [.NET 10](https://dotnet.microsoft.com/) + WPF
- [Azure.AI.Translation.Document](https://www.nuget.org/packages/Azure.AI.Translation.Document)
- [Markdig](https://github.com/xoofx/markdig) & [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net) for Markdown round-tripping

## 🤝 Contributing

Issues and pull requests are welcome! Please open an issue first if you'd like to discuss a larger change.

## 📄 License

Licensed under the terms of the [LICENSE](LICENSE.txt) file in this repository.
