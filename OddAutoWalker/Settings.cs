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
        public double HighAttackSpeedThreshold { get; set; } = 3.0;  // 高攻速阈值
        public double LowAttackSpeedThreshold { get; set; } = 1.2;   // 低攻速阈值
        

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
            HighAttackSpeedThreshold = loadedSettings.HighAttackSpeedThreshold;
            LowAttackSpeedThreshold = loadedSettings.LowAttackSpeedThreshold;
        }
    }
}
