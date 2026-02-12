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

        // 聊天模式状态（Enter 切换，Escape 重置）
        private static bool _isChatMode = false;

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
            // 游戏窗口非前台时，停用走A并忽略所有按键
            if (!GameStateManager.HasLeagueProcess() ||
                !InputController.IsLeagueWindowActive(GameStateManager.GetLeagueProcess()))
            {
                if (OrbWalkerCore.IsOrbWalkerActive())
                {
                    OrbWalkerCore.DeactivateOrbWalker();
                }
                return;
            }

            // 聊天模式检测：仅响应 Down 事件，避免按键重复导致多次切换
            if (state == KeyState.Down)
            {
                if (key == (VirtualKeyCode)0x0D)        // Enter：切换聊天状态
                {
                    _isChatMode = !_isChatMode;
                    // 进入聊天时强制停用走A（防止持C时按Enter导致走A卡住）
                    if (_isChatMode && OrbWalkerCore.IsOrbWalkerActive())
                    {
                        OrbWalkerCore.DeactivateOrbWalker();
                    }
                }
                else if (key == (VirtualKeyCode)0x1B)   // Escape：始终退出聊天
                {
                    _isChatMode = false;
                }
            }

            // 聊天模式下忽略激活键
            if (_isChatMode)
            {
                return;
            }

            // 走A激活/停用逻辑
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
