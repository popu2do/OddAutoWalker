using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OddAutoWalker
{
    public class Settings
    {
        public int ActivationKey { get; set; } = (int)VirtualKeyCode.C;
        public double WindupBufferMs { get; set; } = 66.7;
        public double MinInputDelayMs { get; set; } = 33.3;
        public bool EnableLogging { get; set; } = false;
        public int ApiRetryCount { get; set; } = 3;
        public double TimerIntervalMs { get; set; } = 33.3;

        public void CreateNew(string path)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(path)))
            {
                sw.WriteLine("// ========================================");
                sw.WriteLine("// OddAutoWalker 配置文件说明");
                sw.WriteLine("// ========================================");
                sw.WriteLine();
                sw.WriteLine("// ActivationKey: 激活走A功能的按键码");
                sw.WriteLine("//   按住此键时，程序会执行走A操作");
                sw.WriteLine("//   默认值: 67 (C键)");
                sw.WriteLine();
                sw.WriteLine("// WindupBufferMs: 攻击前摇缓冲时间(毫秒)");
                sw.WriteLine("//   防止因FPS、延迟等原因过早取消攻击");
                sw.WriteLine("//   建议值: 50-100ms，默认值: 66.7ms");
                sw.WriteLine();
                sw.WriteLine("// MinInputDelayMs: 最小输入延迟(毫秒)");
                sw.WriteLine("//   两次输入之间的最小间隔时间");
                sw.WriteLine("//   防止输入过于频繁，建议值: 20-50ms，默认值: 33.3ms");
                sw.WriteLine();
                sw.WriteLine("// EnableLogging: 是否启用日志输出");
                sw.WriteLine("//   true: 显示详细运行信息，false: 静默运行");
                sw.WriteLine("//   调试时建议开启，默认值: false");
                sw.WriteLine();
                sw.WriteLine("// ApiRetryCount: API请求重试次数");
                sw.WriteLine("//   获取游戏数据失败时的重试次数");
                sw.WriteLine("//   建议值: 3-5次，默认值: 3");
                sw.WriteLine();
                sw.WriteLine("// TimerIntervalMs: 定时器间隔(毫秒)");
                sw.WriteLine("//   走A逻辑的执行频率，值越小越精确但占用更多CPU");
                sw.WriteLine("//   建议值: 16-50ms，默认值: 33.3ms (约30FPS)");
                sw.WriteLine();
                sw.WriteLine("// ========================================");
                sw.WriteLine("// 按键码对照表 (常用按键)");
                sw.WriteLine("// ========================================");
                sw.WriteLine("// 字母键:");
                sw.WriteLine("//   65 - A, 66 - B, 67 - C, 68 - D, 69 - E");
                sw.WriteLine("//   70 - F, 71 - G, 72 - H, 73 - I, 74 - J");
                sw.WriteLine("//   75 - K, 76 - L, 77 - M, 78 - N, 79 - O");
                sw.WriteLine("//   80 - P, 81 - Q, 82 - R, 83 - S, 84 - T");
                sw.WriteLine("//   85 - U, 86 - V, 87 - W, 88 - X, 89 - Y, 90 - Z");
                sw.WriteLine();
                sw.WriteLine("// 数字键:");
                sw.WriteLine("//   48 - 0, 49 - 1, 50 - 2, 51 - 3, 52 - 4");
                sw.WriteLine("//   53 - 5, 54 - 6, 55 - 7, 56 - 8, 57 - 9");
                sw.WriteLine();
                sw.WriteLine("// 功能键:");
                sw.WriteLine("//   32 - 空格, 13 - 回车, 27 - ESC, 9 - Tab");
                sw.WriteLine("//   16 - Shift, 17 - Ctrl, 18 - Alt");
                sw.WriteLine();
                sw.WriteLine("// 鼠标侧键:");
                sw.WriteLine("//   4 - 鼠标侧键1, 5 - 鼠标侧键2");
                sw.WriteLine();
                sw.WriteLine("// ========================================");
                sw.WriteLine("// 配置示例:");
                sw.WriteLine("//   - 使用鼠标侧键1激活: \"ActivationKey\": 4");
                sw.WriteLine("//   - 使用空格键激活: \"ActivationKey\": 32");
                sw.WriteLine("//   - 高精度模式: \"TimerIntervalMs\": 16.67");
                sw.WriteLine("//   - 低延迟模式: \"MinInputDelayMs\": 20");
                sw.WriteLine("// ========================================");
                sw.WriteLine();
                sw.WriteLine(JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        public void Load(string path)
        {
            string content = File.ReadAllText(path);
            
            // 移除注释行，只保留JSON内容
            var lines = content.Split('\n');
            var jsonLines = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!trimmedLine.StartsWith("//") && !string.IsNullOrEmpty(trimmedLine))
                {
                    jsonLines.Add(line);
                }
            }
            
            string jsonContent = string.Join("\n", jsonLines);
            var loadedSettings = JsonSerializer.Deserialize<Settings>(jsonContent);
            
            ActivationKey = loadedSettings.ActivationKey;
            
            // 向后兼容：如果新字段不存在，使用默认值
            WindupBufferMs = loadedSettings.WindupBufferMs > 0 ? loadedSettings.WindupBufferMs : 66.7;
            MinInputDelayMs = loadedSettings.MinInputDelayMs > 0 ? loadedSettings.MinInputDelayMs : 33.3;
            EnableLogging = loadedSettings.EnableLogging;
            ApiRetryCount = loadedSettings.ApiRetryCount > 0 ? loadedSettings.ApiRetryCount : 3;
            TimerIntervalMs = loadedSettings.TimerIntervalMs > 0 ? loadedSettings.TimerIntervalMs : 33.3;
        }
    }
}
