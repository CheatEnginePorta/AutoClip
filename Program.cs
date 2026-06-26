using Edge_tts_sharp;
using Edge_tts_sharp.Model;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

// ========== 配置加载 ==========
string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
string projectDir = AppDomain.CurrentDomain.BaseDirectory;

// 默认配置
var config = new Config
{
    FfmpegPath = @"C:\software\ffmpeg\bin\ffmpeg.exe",
    VoiceName = "Xiaoxiao",
    Rate = 0,
    FontName = "Microsoft YaHei",
    FontSize = 48,
    MaxCharsPerLine = 25,
    SubtitleMarginV = 30,
    SubtitleAlignment = 2
};

if (File.Exists(configFile))
{
    try
    {
        string json = File.ReadAllText(configFile, Encoding.UTF8);
        var loaded = JsonSerializer.Deserialize<Config>(json);
        if (loaded != null) config = loaded;
        Console.WriteLine("✅ 已加载配置文件");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ 配置文件读取失败，使用默认配置: {ex.Message}");
    }
}
else
{
    // 生成默认配置文件
    string defaultJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(configFile, defaultJson, Encoding.UTF8);
    Console.WriteLine($"📝 已生成默认配置文件: {configFile}");
    Console.WriteLine("   请修改配置后重新运行程序。");
    Console.ReadKey();
    return;
}

// 文件路径
string textFile = Path.Combine(projectDir, "script.txt");
string videoFile = Path.Combine(projectDir, "source.mp4");
string audioFile = Path.Combine(projectDir, "audio.mp3");
string srtFile = Path.Combine(projectDir, "subtitle.srt");
string assFile = Path.Combine(projectDir, "subtitle.ass");
string outputFile = Path.Combine(projectDir, "output.mp4");

// ========== 第一步：读取文稿 ==========
if (!File.Exists(textFile))
{
    Console.WriteLine($"❌ 找不到文稿文件: {textFile}");
    Console.WriteLine("请创建 script.txt，每行一句话。");
    Console.ReadKey();
    return;
}

string[] lines = File.ReadAllLines(textFile, Encoding.UTF8)
    .Where(l => !string.IsNullOrWhiteSpace(l))
    .ToArray();

Console.WriteLine($"📄 读取到 {lines.Length} 句话");

// ========== 第二步：获取语音对象 ==========
Console.WriteLine("🔍 获取语音列表...");
var allVoices = Edge_tts.GetVoice();
Console.WriteLine($"   共找到 {allVoices.Count()} 个语音");

var voice = allVoices.FirstOrDefault(v => v.Name.Contains(config.VoiceName));
if (voice == null)
{
    Console.WriteLine($"❌ 找不到 {config.VoiceName} 语音，使用第一个可用语音");
    voice = allVoices.First();
}
Console.WriteLine($"🎤 使用语音: {voice.Name}");

// ========== 第三步：生成配音 ==========
string fullText = string.Join("。", lines) + "。";
Console.WriteLine($"🎤 正在生成配音...");
Console.WriteLine($"   文本长度: {fullText.Length} 字符");
Console.WriteLine($"   输出路径: {audioFile}");

if (File.Exists(audioFile)) File.Delete(audioFile);

Edge_tts.Debug = true;
Edge_tts.Await = true;

var option = new PlayOption
{
    Text = fullText,
    Rate = config.Rate,
    SavePath = audioFile
};

Console.WriteLine("   [调试] 调用 Invoke 接收音频数据...");

List<byte> allAudioData = new List<byte>();

Edge_tts.Invoke(option, voice, binaryData =>
{
    Console.WriteLine($"   [调试] 收到音频数据块: {binaryData.Count} 字节");
    allAudioData.AddRange(binaryData);
});

Console.WriteLine("   [调试] 等待音频数据接收...");
int maxWait = 300;
int waited = 0;
while (waited < maxWait)
{
    Thread.Sleep(100);
    waited++;

    if (File.Exists(audioFile) && new FileInfo(audioFile).Length > 0)
    {
        Console.WriteLine($"   [调试] 第 {waited * 0.1:F1} 秒: 文件已自动保存，大小 {new FileInfo(audioFile).Length} 字节");
        break;
    }

    if (waited % 50 == 0)
    {
        Console.WriteLine($"   [调试] 等待中... {waited * 0.1:F1}秒，已收集音频数据: {allAudioData.Count} 字节");
    }
}

if (!File.Exists(audioFile) || new FileInfo(audioFile).Length == 0)
{
    if (allAudioData.Count > 0)
    {
        Console.WriteLine($"   [调试] 手动保存音频数据: {allAudioData.Count} 字节");
        File.WriteAllBytes(audioFile, allAudioData.ToArray());
    }
    else
    {
        Console.WriteLine("   ⚠️ 没有收到音频数据，尝试 SaveAudio...");
        Edge_tts.SaveAudio(option, voice);
        Thread.Sleep(5000);
    }
}

if (!File.Exists(audioFile) || new FileInfo(audioFile).Length == 0)
{
    Console.WriteLine("❌ 音频生成失败");
    Console.ReadKey();
    return;
}

Console.WriteLine($"✅ 音频文件已保存，大小: {new FileInfo(audioFile).Length} 字节");

float totalDuration = GetAudioDuration(config.FfmpegPath, audioFile);
Console.WriteLine($"⏱️ 配音总时长: {totalDuration:F2}秒");

if (totalDuration <= 0)
{
    Console.WriteLine("⚠️ 音频时长为0");
    Console.ReadKey();
    return;
}

// ========== 第四步：生成字幕（ASS格式，自动换行）==========
Console.WriteLine("🎬 正在生成字幕（ASS格式，自动换行）...");

// 根据每句话的字数比例分配时间
var subtitles = new List<(float StartTime, float EndTime, string Text)>();
float totalChars = lines.Sum(l => l.Length);
float currentTime = 0;

for (int i = 0; i < lines.Length; i++)
{
    float ratio = lines[i].Length / totalChars;
    float duration = ratio * totalDuration;
    float start = currentTime;
    float end = currentTime + duration;
    subtitles.Add((start, end, lines[i]));
    currentTime = end;
}

int videoWidth = 1920;
int videoHeight = 1080;

var assBuilder = new StringBuilder();
assBuilder.AppendLine("[Script Info]");
assBuilder.AppendLine("ScriptType: v4.00+");
assBuilder.AppendLine("PlayResX: 1920");
assBuilder.AppendLine("PlayResY: 1080");
assBuilder.AppendLine("ScaledBorderAndShadow: yes");
assBuilder.AppendLine();
assBuilder.AppendLine("[V4+ Styles]");
assBuilder.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
assBuilder.AppendLine($"Style: Default,{config.FontName},{config.FontSize},&H00FFFFFF,&H000000FF,&H00000000,&H80000000,0,0,0,0,100,100,0,0,1,2,1,{config.SubtitleAlignment},{60},{60},{config.SubtitleMarginV},1");
assBuilder.AppendLine();
assBuilder.AppendLine("[Events]");
assBuilder.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

// 自动换行函数
string WrapText(string text, int maxCharsPerLine)
{
    if (string.IsNullOrEmpty(text)) return text;

    var lines = new List<string>();
    int start = 0;
    while (start < text.Length)
    {
        int len = Math.Min(maxCharsPerLine, text.Length - start);
        if (start + len < text.Length)
        {
            int lastSpace = text.LastIndexOf(' ', start + len - 1, len);
            if (lastSpace > start)
                len = lastSpace - start;
        }
        lines.Add(text.Substring(start, len));
        start += len;
    }
    return string.Join("\\N", lines);
}

foreach (var sub in subtitles)
{
    string startTime = FormatAssTime(sub.StartTime);
    string endTime = FormatAssTime(sub.EndTime);
    string wrappedText = WrapText(sub.Text, config.MaxCharsPerLine);
    assBuilder.AppendLine($"Dialogue: 0,{startTime},{endTime},Default,,0,0,0,,{wrappedText}");
}

File.WriteAllText(assFile, assBuilder.ToString());
Console.WriteLine("✅ ASS字幕生成完成");

// ========== 第五步：合成视频（带进度条）==========
Console.WriteLine("🎬 正在合成视频...");

string ffmpegArgs = $"-stream_loop -1 -i \"{videoFile}\" -i \"{audioFile}\" -vf \"subtitles=subtitle.ass\" -c:v libx264 -c:a aac -shortest -y \"{outputFile}\"";

Console.WriteLine($"   [调试] ffmpeg 参数: {ffmpegArgs}");

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = config.FfmpegPath,
        Arguments = ffmpegArgs,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = projectDir
    }
};

// 进度显示
Console.WriteLine();
Console.WriteLine("⏳ 合成进度:");
int lastProgress = -1;
var progressRegex = new Regex(@"time=(\d+):(\d+):(\d+\.\d+)");

process.ErrorDataReceived += (sender, e) =>
{
    if (e.Data == null) return;

    var match = progressRegex.Match(e.Data);
    if (match.Success)
    {
        int h = int.Parse(match.Groups[1].Value);
        int m = int.Parse(match.Groups[2].Value);
        float s = float.Parse(match.Groups[3].Value);
        float currentSec = h * 3600 + m * 60 + s;

        int progress = (int)(currentSec / totalDuration * 100);
        if (progress > lastProgress)
        {
            lastProgress = progress;
            int barLen = 30;
            int filled = progress * barLen / 100;
            string bar = new string('█', filled) + new string('░', barLen - filled);
            Console.Write($"\r   [{bar}] {progress}%  ({FormatTime(currentSec)} / {FormatTime(totalDuration)})");
        }
    }
};

process.Start();
process.BeginErrorReadLine();
await process.WaitForExitAsync();

Console.WriteLine();
Console.WriteLine();

if (process.ExitCode == 0 && File.Exists(outputFile))
{
    long fileSize = new FileInfo(outputFile).Length;
    Console.WriteLine($"✅ 视频合成完成！输出文件: {outputFile}");
    Console.WriteLine($"   文件大小: {fileSize / 1024 / 1024} MB");
}
else
{
    Console.WriteLine($"❌ 合成失败 (退出码: {process.ExitCode})");
}

// 清理
try
{
    if (File.Exists(audioFile)) File.Delete(audioFile);
    if (File.Exists(srtFile)) File.Delete(srtFile);
    if (File.Exists(assFile)) File.Delete(assFile);
}
catch { }

Console.WriteLine("按任意键退出...");
Console.ReadKey();

// ========== 辅助方法 ==========
string FormatTime(float seconds)
{
    int h = (int)(seconds / 3600);
    int m = (int)((seconds % 3600) / 60);
    float s = seconds % 60;
    return $"{h:D2}:{m:D2}:{s:F3}".Replace(",", ".");
}

string FormatAssTime(float seconds)
{
    int h = (int)(seconds / 3600);
    int m = (int)((seconds % 3600) / 60);
    float s = seconds % 60;
    return $"{h}:{m:D2}:{s:F2}";
}

float GetAudioDuration(string ffmpeg, string audioFile)
{
    var proc = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-i \"{audioFile}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    proc.Start();
    string output = proc.StandardError.ReadToEnd();
    proc.WaitForExit();

    var match = Regex.Match(output, @"Duration: (\d+):(\d+):(\d+\.\d+)");
    if (match.Success)
    {
        int h = int.Parse(match.Groups[1].Value);
        int m = int.Parse(match.Groups[2].Value);
        float s = float.Parse(match.Groups[3].Value);
        return h * 3600 + m * 60 + s;
    }
    return 0;
}

// ========== 配置类 ==========
public class Config
{
    public string FfmpegPath { get; set; }
    public string VoiceName { get; set; }
    public int Rate { get; set; }
    public string FontName { get; set; }
    public int FontSize { get; set; }
    public int MaxCharsPerLine { get; set; }
    public int SubtitleMarginV { get; set; }
    public int SubtitleAlignment { get; set; }
}
