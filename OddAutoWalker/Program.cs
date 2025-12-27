using LowLevelInput.Hooks;
using System;
using System.IO;
using System.Timers;

namespace OddAutoWalker
{
    public class Program
    {
        private const string SettingsFile = @"settings\settings.json";

        // 主要组件
        private static readonly Settings CurrentSettings = new Settings();  // 设置管理器
        private static readonly InputManager InputManager = new InputManager(); // 输入管理器

        public static void Main(string[] args)
        {
            if (!File.Exists(SettingsFile))
            {
                Directory.CreateDirectory("settings");
                CurrentSettings.CreateNew(SettingsFile);
            }
            else
            {
                CurrentSettings.Load(SettingsFile);
            }

            // 初始化各个组件
            GameStateManager.Initialize();
            OrbWalkerCore.Initialize(CurrentSettings);

            Console.Clear();
            Console.CursorVisible = false;

            InputManager.Initialize();
            InputManager.OnKeyboardEvent += InputManager_OnKeyboardEvent;
            InputManager.OnMouseEvent += InputManager_OnMouseEvent;
            
            Timer attackSpeedCacheTimer = new Timer(AttackTimingCalculator.GetTickRateMs());
            attackSpeedCacheTimer.Elapsed += AttackSpeedCacheTimer_Elapsed;

            attackSpeedCacheTimer.Start();
            Console.WriteLine($"Press and hold '{(VirtualKeyCode)CurrentSettings.ActivationKey}' to activate the Orb Walker");

            GameStateManager.CheckLeagueProcess();

            Console.ReadLine();
        }

        private static void InputManager_OnMouseEvent(VirtualKeyCode key, KeyState state, int x, int y)
        {
        }

        private static void InputManager_OnKeyboardEvent(VirtualKeyCode key, KeyState state)
        {
            if (key == (VirtualKeyCode)CurrentSettings.ActivationKey)
            {
                switch (state)
                {
                    case KeyState.Down when !OrbWalkerCore.IsOrbWalkerActive():
                        OrbWalkerCore.ActivateOrbWalker(CurrentSettings);
                        break;

                    case KeyState.Up when OrbWalkerCore.IsOrbWalkerActive():
                        OrbWalkerCore.DeactivateOrbWalker();
                        break;
                }
            }
        }


        private static void AttackSpeedCacheTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 更新攻击速度数据
            GameStateManager.UpdateAttackSpeedData();

#if DEBUG
            Console.SetCursorPosition(0, 0);
            Console.WriteLine(OrbWalkerCore.GetDebugInfo(CurrentSettings));
#endif
        }
    }
}
