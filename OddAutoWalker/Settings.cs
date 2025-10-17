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
        /// <summary>
        /// 激活键
        /// </summary>
        public int ActivationKey { get; set; } = (int)VirtualKeyCode.C;
        
        /// <summary>
        /// 计算刻最大频率（Hz）
        /// 限制系统检查频率的上限，防止CPU占用过高
        /// 默认120Hz，适配240fps显示器的一半频率
        /// </summary>
        public double MaxCheckFrequencyHz { get; set; } = 120;
        
        /// <summary>
        /// 移动指令最小间隔（秒）
        /// 防止移动指令过于频繁导致游戏掉线或无效微操
        /// 当攻速很高时（如10.0以上），间隔再小也没意义，不如站桩输出
        /// </summary>
        public double MinMoveCommandIntervalSeconds { get; set; } = 0.01;
        
        /// <summary>
        /// 移动指令最大间隔（秒）
        /// 防止移动间隔过长影响走位
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
            if (loadedSettings.MaxCheckFrequencyHz != default(double))
                MaxCheckFrequencyHz = loadedSettings.MaxCheckFrequencyHz;
            if (loadedSettings.MinMoveCommandIntervalSeconds != default(double))
                MinMoveCommandIntervalSeconds = loadedSettings.MinMoveCommandIntervalSeconds;
            if (loadedSettings.MaxMoveCommandIntervalSeconds != default(double))
                MaxMoveCommandIntervalSeconds = loadedSettings.MaxMoveCommandIntervalSeconds;
        }
    }
}
