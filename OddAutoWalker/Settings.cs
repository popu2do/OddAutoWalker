using LowLevelInput.Hooks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OddAutoWalker
{
    public class Settings
    {
        public int ActivationKey { get; set; } = (int)VirtualKeyCode.C;
        
        /// <summary>
        /// 是否启用自适应定时器间隔
        /// </summary>
        public bool EnableAdaptiveTimer { get; set; } = true;
        
        /// <summary>
        /// 固定定时器间隔（毫秒），当自适应定时器禁用时使用
        /// </summary>
        public double FixedTimerIntervalMs { get; set; } = 16.67;
        
        /// <summary>
        /// 最小移动指令间隔（秒），防止过于频繁的移动
        /// </summary>
        public double MinMoveIntervalSeconds { get; set; } = 0.1;
        
        /// <summary>
        /// 是否启用智能移动逻辑
        /// </summary>
        public bool EnableSmartMoveLogic { get; set; } = true;
        
        /// <summary>
        /// 移动指令最小间隔（秒），防止过于频繁的移动指令
        /// 当攻速很高时（如10.0以上），间隔再小也没意义，不如站桩输出
        /// </summary>
        public double MinMoveCommandIntervalSeconds { get; set; } = 0.01;
        
        /// <summary>
        /// 移动指令最大间隔（秒），防止移动间隔过长影响走位
        /// 当攻速很低时，限制最大移动间隔，保持基本的走位频率
        /// </summary>
        public double MaxMoveCommandIntervalSeconds { get; set; } = 0.33;

        public void CreateNew(string path)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(path)))
            {
                sw.WriteLine("/* All Corresponding Key Bind Key Codes");
                foreach (int i in Enum.GetValues(typeof(VirtualKeyCode)))
                {
                    sw.WriteLine($"* \t{i} - {(VirtualKeyCode)i}");
                }
                sw.WriteLine("*/");
                sw.WriteLine(JsonConvert.SerializeObject(this));
            }
        }

        public void Load(string path)
        {
            var loadedSettings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            ActivationKey = loadedSettings.ActivationKey;
            
            // 兼容旧版本配置文件
            if (loadedSettings.EnableAdaptiveTimer != default(bool))
                EnableAdaptiveTimer = loadedSettings.EnableAdaptiveTimer;
            if (loadedSettings.FixedTimerIntervalMs != default(double))
                FixedTimerIntervalMs = loadedSettings.FixedTimerIntervalMs;
            if (loadedSettings.MinMoveIntervalSeconds != default(double))
                MinMoveIntervalSeconds = loadedSettings.MinMoveIntervalSeconds;
            if (loadedSettings.EnableSmartMoveLogic != default(bool))
                EnableSmartMoveLogic = loadedSettings.EnableSmartMoveLogic;
            if (loadedSettings.MinMoveCommandIntervalSeconds != default(double))
                MinMoveCommandIntervalSeconds = loadedSettings.MinMoveCommandIntervalSeconds;
            if (loadedSettings.MaxMoveCommandIntervalSeconds != default(double))
                MaxMoveCommandIntervalSeconds = loadedSettings.MaxMoveCommandIntervalSeconds;
        }
    }
}
