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
        public double MinInputDelayMs { get; set; } = -1;  // -1 = auto, or fixed value (10-50)
        public bool EnableLogging { get; set; } = false;
        public int ApiRetryCount { get; set; } = 3;
        public double TimerIntervalMs { get; set; } = -1;  // -1 = auto (4-33ms), or fixed value

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
                sw.WriteLine("// TimerIntervalMs: 走A刷新频率(毫秒)");
                sw.WriteLine("//   控制程序检查攻击/移动的频率，值越小越精确但占用更多CPU");
                sw.WriteLine("//   -1: 智能模式，根据攻速自动调整(5-16.67ms)");
                sw.WriteLine("//   固定值: 5-16.67ms，推荐16.67ms(60Hz)");
                sw.WriteLine();
                sw.WriteLine("// MinInputDelayMs: 按键间隔(毫秒)");
                sw.WriteLine("//   两次按键之间的最小间隔，防止按键过于频繁");
                sw.WriteLine("//   -1: 智能模式，根据攻速自动调整(10-50ms)");
                sw.WriteLine("//   固定值: 10-50ms，推荐16.67ms");
                sw.WriteLine();
                sw.WriteLine("// EnableLogging: 是否显示运行信息");
                sw.WriteLine("//   true: 显示攻速、定时器状态等信息");
                sw.WriteLine("//   false: 静默运行，不显示任何信息");
                sw.WriteLine();
                sw.WriteLine("// ApiRetryCount: 网络重试次数");
                sw.WriteLine("//   获取游戏数据失败时的重试次数");
                sw.WriteLine("//   建议值: 3-5次，网络不好可以增加到5");
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
                sw.WriteLine("// 配置示例 (复制到settings.json使用):");
                sw.WriteLine("// ========================================");
                sw.WriteLine("// 1. 新手推荐配置 (智能模式):");
                sw.WriteLine("// {");
                sw.WriteLine("//   \"ActivationKey\": 67,");
                sw.WriteLine("//   \"TimerIntervalMs\": -1,");
                sw.WriteLine("//   \"MinInputDelayMs\": -1,");
                sw.WriteLine("//   \"EnableLogging\": true,");
                sw.WriteLine("//   \"ApiRetryCount\": 3");
                sw.WriteLine("// }");
                sw.WriteLine();
                sw.WriteLine("// 2. 高精度配置 (追求极致流畅):");
                sw.WriteLine("// {");
                sw.WriteLine("//   \"ActivationKey\": 67,");
                sw.WriteLine("//   \"TimerIntervalMs\": 5.0,");
                sw.WriteLine("//   \"MinInputDelayMs\": 10,");
                sw.WriteLine("//   \"EnableLogging\": true,");
                sw.WriteLine("//   \"ApiRetryCount\": 3");
                sw.WriteLine("// }");
                sw.WriteLine();
                sw.WriteLine("// 3. 平衡配置 (性能与流畅兼顾):");
                sw.WriteLine("// {");
                sw.WriteLine("//   \"ActivationKey\": 67,");
                sw.WriteLine("//   \"TimerIntervalMs\": 16.67,");
                sw.WriteLine("//   \"MinInputDelayMs\": 16.67,");
                sw.WriteLine("//   \"EnableLogging\": true,");
                sw.WriteLine("//   \"ApiRetryCount\": 3");
                sw.WriteLine("// }");
                sw.WriteLine();
                sw.WriteLine("// 4. 鼠标侧键激活:");
                sw.WriteLine("// {");
                sw.WriteLine("//   \"ActivationKey\": 4,");
                sw.WriteLine("//   \"TimerIntervalMs\": -1,");
                sw.WriteLine("//   \"MinInputDelayMs\": -1,");
                sw.WriteLine("//   \"EnableLogging\": true,");
                sw.WriteLine("//   \"ApiRetryCount\": 3");
                sw.WriteLine("// }");
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
            MinInputDelayMs = loadedSettings.MinInputDelayMs >= 0 ? loadedSettings.MinInputDelayMs : -1;
            EnableLogging = loadedSettings.EnableLogging;
            ApiRetryCount = loadedSettings.ApiRetryCount > 0 ? loadedSettings.ApiRetryCount : 3;
            TimerIntervalMs = loadedSettings.TimerIntervalMs >= 0 ? loadedSettings.TimerIntervalMs : -1;
        }
    }
}
