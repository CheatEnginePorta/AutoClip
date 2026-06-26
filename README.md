# AutoClip 🎬

一键将文稿转为配音视频。读取文本 → Edge TTS 配音 → 自动生成字幕 → ffmpeg 合成视频，全自动流水线。

## ✨ 功能

- 📄 读取 `script.txt` 文稿（每行一句话）
- 🎤 调用 **Edge TTS** 生成自然语音配音
- 📝 自动生成 **ASS 字幕**（支持自动换行，防止溢出）
- 🎞️ 用 **ffmpeg** 合成最终视频（带进度条显示）
- ⚙️ 通过 `config.json` 灵活配置

## 🚀 快速开始
### 3. 准备素材

在程序目录下放入以下文件：

| 文件 | 说明 |
|------|------|
| `script.txt` | 文稿文件，**每行一句话**（UTF-8 编码） |
| `source.mp4` | 背景视频素材（将循环播放） |

### 4. 运行

```bash
dotnet run
# 或直接运行编译后的 exe
```

**首次运行**会自动生成 `config.json` 配置文件，然后退出。修改配置后重新运行即可。

## ⚙️ 配置说明

编辑 `config.json`：

```json
{
  "FfmpegPath": "C:\\software\\ffmpeg\\bin\\ffmpeg.exe",
  "VoiceName": "Xiaoxiao",
  "Rate": 0,
  "FontName": "Microsoft YaHei",
  "FontSize": 48,
  "MaxCharsPerLine": 25,
  "SubtitleMarginV": 30,
  "SubtitleAlignment": 2
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `FfmpegPath` | ffmpeg 可执行文件路径 | `C:\software\ffmpeg\bin\ffmpeg.exe` |
| `VoiceName` | Edge TTS 语音名称（如 Xiaoxiao, Yunyang, Yunxi 等） | `Xiaoxiao` |
| `Rate` | 语速调节（-100 ~ 100，0 为正常） | `0` |
| `FontName` | 字幕字体 | `Microsoft YaHei` |
| `FontSize` | 字幕字号 | `48` |
| `MaxCharsPerLine` | 每行最大字符数（超长自动换行） | `25` |
| `SubtitleMarginV` | 字幕垂直边距 | `30` |
| `SubtitleAlignment` | 字幕对齐方式（1=左, 2=居中, 3=右） | `2` |

## 📂 输出

运行成功后，在程序目录下生成 `output.mp4`，即最终合成视频。

## 🧩 工作流程

```
script.txt ──→ Edge TTS ──→ audio.mp3
                              │
source.mp4 ──→ ffmpeg 合成 ──→ output.mp4
                  │
              subtitle.ass (自动生成，自动换行)
```

## 📝 注意事项

- `script.txt` 必须使用 **UTF-8 编码**
- 背景视频 `source.mp4` 会循环播放，时长由配音长度决定
- 字幕按每句话的字数比例自动分配时间轴
- 程序运行结束后会自动清理临时文件（音频、字幕）

## 🛠️ 技术栈

- [Edge-TTS-sharp] — 调用微软 Edge TTS 服务
- [ffmpeg](https://ffmpeg.org/) — 视频合成
- .NET 8.0 Console Application

## 📄 许可证

MIT
