using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace OddAutoWalker
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public enum VirtualKeyCode
    {
        C = 0x43,        // 默认走A激活键（可在配置中修改）
        RETURN = 0x0D,   // 回车键 - 进入聊天模式
        ESCAPE = 0x1B    // ESC键 - 退出聊天模式
    }

    public enum KeyState
    {
        Down,
        Up
    }

    public class InputManager
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public event Action<VirtualKeyCode, KeyState> OnKeyboardEvent;

        private bool[] keyStates = new bool[256];
        private System.Timers.Timer keyCheckTimer;

        public void Initialize()
        {
            keyCheckTimer = new System.Timers.Timer(10); // 检查频率 10ms
            keyCheckTimer.Elapsed += CheckKeys;
            keyCheckTimer.Start();
        }

        private void CheckKeys(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < 256; i++)
            {
                bool isPressed = (GetAsyncKeyState(i) & 0x8000) != 0;
                
                if (isPressed != keyStates[i])
                {
                    keyStates[i] = isPressed;
                    
                    if (Enum.IsDefined(typeof(VirtualKeyCode), i))
                    {
                        var keyCode = (VirtualKeyCode)i;
                        var state = isPressed ? KeyState.Down : KeyState.Up;
                        OnKeyboardEvent?.Invoke(keyCode, state);
                    }
                }
            }
        }

        public void Dispose()
        {
            keyCheckTimer?.Stop();
            keyCheckTimer?.Dispose();
        }
    }

    public class Program
    {
        private const string SettingsFile = @"settings\settings.json";
        private const double OrderTickRate = 1d / 30d;

        private static readonly Settings CurrentSettings = new Settings();
        private static readonly InputManager InputManager = new InputManager();
        private static readonly GameStateManager GameStateManager = new GameStateManager();
        private static readonly ApiManager ApiManager = new ApiManager();
        private static readonly ChampionDataManager ChampionDataManager = new ChampionDataManager(ApiManager);
        private static readonly OrbWalkEngine OrbWalkEngine = new OrbWalkEngine(GameStateManager, ChampionDataManager, CurrentSettings);

        private static System.Timers.Timer AttackSpeedCacheTimer;
        
        // 自适应计算函数
        private static double GetAdaptiveTimerInterval()
        {
            // 基于攻击间隔的合理算法
            // 目标：每次攻击至少有8-12个检查周期，确保精确度
            var secondsPerAttack = ChampionDataManager.GetSecondsPerAttack();
            
            // 计算理想的检查频率：每次攻击8-12次检查
            var idealChecksPerAttack = 10; // 每次攻击检查10次
            var idealInterval = secondsPerAttack / idealChecksPerAttack * 1000;
            
            // 限制范围：最高200Hz(5ms)，最低60Hz(16.67ms)
            // 200Hz足够精确，60Hz保证基本流畅
            return Math.Max(Math.Min(idealInterval, 5.0), 16.67);
        }
        
        // 获取有效参数值（支持auto模式）
        private static double GetEffectiveTimerInterval()
        {
            return CurrentSettings.TimerIntervalMs < 0 
                ? GetAdaptiveTimerInterval() 
                : CurrentSettings.TimerIntervalMs;
        }

        private static void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            if (!CurrentSettings.EnableLogging) return;
            
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var levelStr = level.ToString().ToUpper();
            Console.WriteLine($"[{timestamp}] [{levelStr}] {message}");
        }

        private static void Cleanup()
        {
            LogMessage("正在清理资源...", LogLevel.Info);
            ApiManager?.Dispose();
            OrbWalkEngine?.Dispose();
            LogMessage("资源清理完成", LogLevel.Info);
        }

        private static async Task StatusDisplayLoop()
        {
            while (!GameStateManager.IsExiting)
            {
                try
                {
                    if (CurrentSettings.EnableLogging)
                    {
                        var status = OrbWalkEngine.IsActive ? "激活" : "未激活";
                        var gameStatus = GameStateManager.IsGameActive() ? "游戏中" : "游戏外";
                        var chatStatus = GameStateManager.IsInChatMode ? "聊天中" : "正常";
                        var attackSpeed = ChampionDataManager.ClientAttackSpeed.ToString("F3");
                        var windupTime = ChampionDataManager.GetWindupDuration().ToString("F3");
                        
                        var timerInfo = CurrentSettings.TimerIntervalMs < 0 
                            ? $"{GetEffectiveTimerInterval():F2}ms (auto)" 
                            : $"{CurrentSettings.TimerIntervalMs:F2}ms";
                        
                        Console.SetCursorPosition(0, 3);
                        Console.WriteLine($"状态: {status} | 游戏: {gameStatus} | 聊天: {chatStatus} | 攻速: {attackSpeed} | 定时器: {timerInfo} | 网络: {ApiManager.EstimatedApiLatency:F0}ms");
                    }
                    
                    await Task.Delay(500); // 每500ms更新一次状态
                }
                catch (Exception ex)
                {
                    LogMessage($"状态显示错误: {ex.Message}", LogLevel.Error);
                    await Task.Delay(1000);
                }
            }
        }

        public static void Main(string[] args)
        {
            // 初始化设置
            InitializeSettings();

            // 设置控制台
            Console.Clear();
            Console.CursorVisible = false;

            // 初始化组件
            InitializeComponents();

            // 设置事件处理
            SetupEventHandlers();

            // 启动定时器
            StartTimers();

            // 显示启动信息
            Console.WriteLine($"Press and hold '{(VirtualKeyCode)CurrentSettings.ActivationKey}' to activate the Orb Walker");

            // 启动后台任务
            _ = Task.Run(CheckLeagueProcess);
            _ = Task.Run(StatusDisplayLoop);

            // 保持程序运行
            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void InitializeSettings()
        {
            if (!File.Exists(SettingsFile))
            {
                Directory.CreateDirectory("settings");
                CurrentSettings.CreateNew(SettingsFile);
                LogMessage("创建新的配置文件", LogLevel.Info);
            }
            else
            {
                CurrentSettings.Load(SettingsFile);
                LogMessage("加载配置文件成功", LogLevel.Info);
            }
        }

        private static void InitializeComponents()
        {
            InputManager.Initialize();
            OrbWalkEngine.Initialize(GetEffectiveTimerInterval());
        }

        private static void SetupEventHandlers()
        {
            InputManager.OnKeyboardEvent += InputManager_OnKeyboardEvent;
            GameStateManager.OnProcessDetected += OnProcessDetected;
            GameStateManager.OnProcessLost += OnProcessLost;
            ApiManager.OnLogMessage += LogMessage;
            ChampionDataManager.OnLogMessage += LogMessage;
            OrbWalkEngine.OnLogMessage += LogMessage;
        }

        private static void StartTimers()
        {
            AttackSpeedCacheTimer = new System.Timers.Timer(OrderTickRate);
            AttackSpeedCacheTimer.Elapsed += AttackSpeedCacheTimer_Elapsed;
            AttackSpeedCacheTimer.Start();
        }

        private static void OnProcessDetected()
        {
            LogMessage("游戏进程检测成功", LogLevel.Info);
        }

        private static void OnProcessLost()
        {
            LogMessage("游戏进程已退出，正在重新检测...", LogLevel.Warning);
            ChampionDataManager.Reset();
            ApiManager.ResetFailureCount();
            CheckLeagueProcess();
        }


        private static void InputManager_OnKeyboardEvent(VirtualKeyCode key, KeyState state)
        {
            // 处理回车键 - 进入聊天模式
            if (key == VirtualKeyCode.RETURN && state == KeyState.Down)
            {
                GameStateManager.IsInChatMode = true;
                GameStateManager.LastChatActivity = DateTime.Now;
                LogMessage("进入聊天模式", LogLevel.Info);
                return;
            }
            
            // 处理ESC键 - 退出聊天模式
            if (key == VirtualKeyCode.ESCAPE && state == KeyState.Down)
            {
                GameStateManager.IsInChatMode = false;
                LogMessage("退出聊天模式", LogLevel.Info);
                return;
            }
            
            // 处理走A激活键 - 延迟激活走A功能
            if (key == (VirtualKeyCode)CurrentSettings.ActivationKey)
            {
                switch (state)
                {
                    case KeyState.Down:
                        GameStateManager.OrbWalkKeyPressStart = DateTime.Now;
                        GameStateManager.OrbWalkKeyPressed = true;
                        break;
                        
                    case KeyState.Up:
                        GameStateManager.OrbWalkKeyPressed = false;
                        // 如果走A已经激活，松开激活键时停用
                        if (OrbWalkEngine.IsActive)
                        {
                            OrbWalkEngine.Stop();
                        }
                        break;
                }
            }
        }


        private static async void CheckLeagueProcess()
        {
            LogMessage("正在检测游戏进程...", LogLevel.Info);
            while (GameStateManager.LeagueProcess is null || !GameStateManager.HasProcess)
            {
                var process = Process.GetProcessesByName("League of Legends").FirstOrDefault();
                if (process is null || process.HasExited)
                {
                    LogMessage("未检测到游戏进程，等待中...", LogLevel.Warning);
                    await Task.Delay(2000); // 添加2秒延迟，避免疯狂循环
                    continue;
                }
                GameStateManager.SetProcess(process);
            }
        }


        private static async void AttackSpeedCacheTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!GameStateManager.HasProcess || GameStateManager.IsExiting)
                return;

            // 初始化英雄数据
            if (string.IsNullOrEmpty(ChampionDataManager.ChampionName))
            {
                await ChampionDataManager.InitializeChampionData();
            }
            else
            {
                // 更新攻速
                await ChampionDataManager.UpdateAttackSpeed();
                
                // 如果启用自适应且攻速改变，重新设置定时器
                if (CurrentSettings.TimerIntervalMs < 0)
                {
                    var newInterval = GetAdaptiveTimerInterval();
                    OrbWalkEngine.UpdateTimerInterval(newInterval);
                }
            }
        }

    }
}
