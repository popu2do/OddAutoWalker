using System.Text.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Timers;
using System.Net;

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
        C = 0x43
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
        private Timer keyCheckTimer;

        public void Initialize()
        {
            keyCheckTimer = new Timer(10); // 检查频率 10ms
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
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const string ActivePlayerEndpoint = @"https://127.0.0.1:2999/liveclientdata/activeplayer";
        private const string PlayerListEndpoint = @"https://127.0.0.1:2999/liveclientdata/playerlist";
        private const string ChampionStatsEndpoint = @"https://raw.communitydragon.org/latest/game/data/characters/";
        private const string SettingsFile = @"settings\settings.json";

        private static bool HasProcess = false;
        private static bool IsExiting = false;
        private static bool IsIntializingValues = false;
        private static bool IsUpdatingAttackValues = false;

        private static readonly Settings CurrentSettings = new Settings();
        private static readonly HttpClient Client = CreateHttpClient();
        private static readonly InputManager InputManager = new InputManager();
        private static Process LeagueProcess = null;

        private static Timer OrbWalkTimer;

        private static bool OrbWalkerTimerActive = false;

        private static string ActivePlayerName = string.Empty;
        private static string ChampionName = string.Empty;
        private static string RawChampionName = string.Empty;

        private static double ClientAttackSpeed = 0.625;
        private static double ChampionAttackCastTime = 0.625;
        private static double ChampionAttackTotalTime = 0.625;
        private static double ChampionAttackSpeedRatio = 0.625;
        private static double ChampionAttackDelayPercent = 0.3;
        private static double ChampionAttackDelayScaling = 1.0;

        /// <summary>
        /// This is a buffer to prevent you from accidentally canceling your auto-attack too soon, as a result of fps, ping, or otherwise.
        /// </summary>
        private static readonly double WindupBuffer = 1d / 15d;

        // If we're trying to input faster than this, don't
        private static double MinInputDelay => GetEffectiveMinInputDelay();

        // This is honestly just semi-random because we need an interval to run the timer at
        private static readonly double OrderTickRate = 1d / 30d;

#if DEBUG
        private static int TimerCallbackCounter = 0;
#endif

        // These are all in seconds
        public static double GetSecondsPerAttack() => 1 / ClientAttackSpeed;
        public static double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;
        public static double GetBufferedWindupDuration() => GetWindupDuration();
        
        // 自适应计算函数
        private static double GetAdaptiveTimerInterval()
        {
            // 基于攻击间隔的合理算法
            // 目标：每次攻击至少有8-12个检查周期，确保精确度
            var secondsPerAttack = GetSecondsPerAttack();
            
            // 计算理想的检查频率：每次攻击8-12次检查
            var idealChecksPerAttack = 10; // 每次攻击检查10次
            var idealInterval = secondsPerAttack / idealChecksPerAttack * 1000;
            
            // 限制范围：最高200Hz(5ms)，最低60Hz(16.67ms)
            // 200Hz足够精确，60Hz保证基本流畅
            return Math.Max(Math.Min(idealInterval, 5.0), 16.67);
        }
        
        private static double GetAdaptiveInputDelay()
        {
            // 基于攻速的最小输入间隔
            var secondsPerAttack = GetSecondsPerAttack();
            return Math.Max(secondsPerAttack / 20.0 * 1000, 10); // 最少10ms
        }
        
        // 获取有效参数值（支持auto模式）
        private static double GetEffectiveTimerInterval()
        {
            return CurrentSettings.TimerIntervalMs < 0 
                ? GetAdaptiveTimerInterval() 
                : CurrentSettings.TimerIntervalMs;
        }
        
        private static double GetEffectiveMinInputDelay()
        {
            return CurrentSettings.MinInputDelayMs < 0 
                ? GetAdaptiveInputDelay() / 1000.0
                : CurrentSettings.MinInputDelayMs / 1000.0;
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            return new HttpClient(handler);
        }

        private static void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            if (!CurrentSettings.EnableLogging) return;
            
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var levelStr = level.ToString().ToUpper();
            Console.WriteLine($"[{timestamp}] [{levelStr}] {message}");
        }

        private static bool IsGameActive()
        {
            return HasProcess && !IsExiting && GetForegroundWindow() == LeagueProcess?.MainWindowHandle;
        }

        private static void Cleanup()
        {
            LogMessage("正在清理资源...", LogLevel.Info);
            Client?.Dispose();
            OrbWalkTimer?.Stop();
            LogMessage("资源清理完成", LogLevel.Info);
        }

        private static async Task StatusDisplayLoop()
        {
            while (!IsExiting)
            {
                try
                {
                    if (CurrentSettings.EnableLogging)
                    {
                        var status = OrbWalkerTimerActive ? "激活" : "未激活";
                        var gameStatus = IsGameActive() ? "游戏中" : "游戏外";
                        var attackSpeed = ClientAttackSpeed.ToString("F3");
                        var windupTime = GetWindupDuration().ToString("F3");
                        
                        var timerInfo = CurrentSettings.TimerIntervalMs < 0 
                            ? $"{GetEffectiveTimerInterval():F2}ms (auto)" 
                            : $"{CurrentSettings.TimerIntervalMs:F2}ms";
                        
                        Console.SetCursorPosition(0, 3);
                        Console.WriteLine($"状态: {status} | 游戏: {gameStatus} | 攻速: {attackSpeed} | 定时器: {timerInfo} | 网络: {estimatedApiLatency:F0}ms");
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

            Console.Clear();
            Console.CursorVisible = false;

            InputManager.Initialize();
            InputManager.OnKeyboardEvent += InputManager_OnKeyboardEvent;

            // 初始化定时器
            OrbWalkTimer = new Timer(GetEffectiveTimerInterval());
            OrbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
#if DEBUG
            Timer callbackTimer = new Timer(16.66);
            callbackTimer.Elapsed += Timer_CallbackLog;
#endif

            Timer attackSpeedCacheTimer = new Timer(OrderTickRate);
            attackSpeedCacheTimer.Elapsed += AttackSpeedCacheTimer_Elapsed;

            attackSpeedCacheTimer.Start();
            Console.WriteLine($"Press and hold '{(VirtualKeyCode)CurrentSettings.ActivationKey}' to activate the Orb Walker");

            CheckLeagueProcess();

            // 启动状态显示循环
            _ = Task.Run(StatusDisplayLoop);

            Console.WriteLine($"按任意键退出程序...");
            Console.ReadLine();
            
            // 程序退出时清理资源
            Cleanup();
        }

#if DEBUG
        private static void Timer_CallbackLog(object sender, ElapsedEventArgs e)
        {
            if (TimerCallbackCounter > 1 || TimerCallbackCounter < 0)
            {
                Console.Clear();
                Console.WriteLine("Timer Error Detected");
                throw new Exception("Timers must not run simultaneously");
            }
        }

        private static void LogTimerPerformance()
        {
            if (owStopWatch.IsRunning)
            {
                var elapsed = owStopWatch.ElapsedMilliseconds;
                if (elapsed > CurrentSettings.TimerIntervalMs * 1.5) // 如果执行时间超过预期间隔的1.5倍
                {
                    LogMessage($"定时器性能警告: 执行时间 {elapsed}ms，预期 {CurrentSettings.TimerIntervalMs}ms", LogLevel.Warning);
                }
            }
        }
#endif

        private static void InputManager_OnKeyboardEvent(VirtualKeyCode key, KeyState state)
        {
            if (key == (VirtualKeyCode)CurrentSettings.ActivationKey)
            {
                switch (state)
                {
                    case KeyState.Down when !OrbWalkerTimerActive:
                        OrbWalkerTimerActive = true;
                        OrbWalkTimer.Start();
                        LogMessage("走A功能已激活", LogLevel.Info);
                        break;

                    case KeyState.Up when OrbWalkerTimerActive:
                        OrbWalkerTimerActive = false;
                        OrbWalkTimer.Stop();
                        LogMessage("走A功能已停用", LogLevel.Info);
                        break;
                }
            }
        }

        // When these DateTime instances are in the past, the action they gate can be taken
        private static DateTime nextInput = default;
        private static DateTime nextMove = default;
        private static DateTime nextAttack = default;

        private static readonly Stopwatch owStopWatch = new Stopwatch();

        private static void OrbWalkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            owStopWatch.Start();
            TimerCallbackCounter++;
#endif
            if (!IsGameActive())
            {
#if DEBUG
                TimerCallbackCounter--;
                LogTimerPerformance();
#endif
                return;
            }

            // Store time at timer tick start into a variable for readability
            var time = e.SignalTime;

            // Make sure we can send input without being dropped
            // This is used for gating movement orders when waiting for an attack to be prepared
            // This is not needed if this function is not ran frequently enough for it to matter
            // If it isn't, you might end up with this timer and this function's timer being out of sync
            //   resulting in a (worst-case) OrderTickRate + MinInputDelay delay
            // It is currently disabled due to this, enable it if you want/need to
            if (true || nextInput < time)
            {
                // If we can attack, do so
                if (nextAttack < time)
                {
                    // Store current time + input delay so we're aware when we can move next
                    nextInput = time.AddSeconds(MinInputDelay);

                    // Send attack input
                    InputSimulator.Keyboard.KeyDown((ushort)DirectInputKeys.DIK_A);
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Left);
                    InputSimulator.Keyboard.KeyUp((ushort)DirectInputKeys.DIK_A);

                    // We've sent input now, so we're re-fetching time as I have no idea how long input takes
                    // I'm assuming it's negligable, but why not
                    // Please check what the actual difference is if you consider keeping this lol
                    var attackTime = DateTime.Now;

                    // Store timings for when to next attack / move
                    nextMove = attackTime.AddSeconds(GetBufferedWindupDuration());
                    nextAttack = attackTime.AddSeconds(GetSecondsPerAttack());
                }
                // If we can't attack but we can move, do so
                else if (nextMove < time)
                {
                    // Store current time + input delay so we're aware when we can attack / move next
                    nextInput = time.AddSeconds(MinInputDelay);

                    // Send move input
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Right);
                }
            }
#if DEBUG
            TimerCallbackCounter--;
            LogTimerPerformance();
            owStopWatch.Reset();
#endif
        }

        private static void CheckLeagueProcess()
        {
            while (LeagueProcess is null || !HasProcess)
            {
                LeagueProcess = Process.GetProcessesByName("League of Legends").FirstOrDefault();
                if (LeagueProcess is null || LeagueProcess.HasExited)
                {
                    continue;
                }
                HasProcess = true;
                LeagueProcess.EnableRaisingEvents = true;
                LeagueProcess.Exited += LeagueProcess_Exited;
            }
        }

        private static void LeagueProcess_Exited(object sender, EventArgs e)
        {
            HasProcess = false;
            LeagueProcess = null;
            ResetGameState();
            LogMessage("游戏进程已退出，正在重新检测...", LogLevel.Warning);
            CheckLeagueProcess();
        }

        private static void ResetGameState()
        {
            // 重置游戏相关状态
            ActivePlayerName = string.Empty;
            ChampionName = string.Empty;
            RawChampionName = string.Empty;
            ClientAttackSpeed = 0.625;
            ChampionAttackCastTime = 0.625;
            ChampionAttackTotalTime = 0.625;
            ChampionAttackSpeedRatio = 0.625;
            ChampionAttackDelayPercent = 0.3;
            ChampionAttackDelayScaling = 1.0;
            
            // 重置API失败计数
            apiFailureCount = 0;
            lastApiFailure = DateTime.MinValue;
            
            LogMessage("游戏状态已重置", LogLevel.Info);
        }

        private static int apiFailureCount = 0;
        private static DateTime lastApiFailure = DateTime.MinValue;
        
        // 网络延迟检测
        private static double estimatedApiLatency = 30; // 默认30ms

        private static async Task<JsonDocument> GetApiDataWithRetry(string endpoint, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var response = await Client.GetStringAsync(endpoint);
                    sw.Stop();
                    
                    // 更新网络延迟估计（移动平均）
                    estimatedApiLatency = estimatedApiLatency * 0.8 + sw.ElapsedMilliseconds * 0.2;
                    
                    apiFailureCount = 0; // 重置失败计数
                    return JsonDocument.Parse(response);
                }
                catch (HttpRequestException ex)
                {
                    LogMessage($"网络请求失败 (尝试 {i + 1}/{maxRetries}): {endpoint}, 错误: {ex.Message}", LogLevel.Warning);
                    
                    if (i == maxRetries - 1)
                    {
                        apiFailureCount++;
                        lastApiFailure = DateTime.Now;
                        if (apiFailureCount >= 5)
                        {
                            LogMessage($"API连续失败 {apiFailureCount} 次，请检查游戏是否正常运行", LogLevel.Error);
                            // 如果连续失败太多次，尝试重置游戏状态
                            if (apiFailureCount >= 10)
                            {
                                LogMessage("检测到持续API失败，尝试重置游戏状态", LogLevel.Warning);
                                ResetGameState();
                            }
                        }
                        return null;
                    }
                    
                    await Task.Delay(100 * (i + 1)); // 递增延迟
                }
                catch (TaskCanceledException)
                {
                    LogMessage($"API请求超时 (尝试 {i + 1}/{maxRetries}): {endpoint}", LogLevel.Warning);
                    
                    if (i == maxRetries - 1)
                    {
                        apiFailureCount++;
                        lastApiFailure = DateTime.Now;
                        return null;
                    }
                    
                    await Task.Delay(200 * (i + 1)); // 超时后更长的延迟
                }
                catch (Exception ex)
                {
                    LogMessage($"API调用异常 (尝试 {i + 1}/{maxRetries}): {endpoint}, 错误: {ex.Message}", LogLevel.Error);
                    
                    if (i == maxRetries - 1)
                    {
                        apiFailureCount++;
                        lastApiFailure = DateTime.Now;
                        return null;
                    }
                    
                    await Task.Delay(100 * (i + 1));
                }
            }
            return null;
        }

        private static async void AttackSpeedCacheTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (HasProcess && !IsExiting && !IsIntializingValues && !IsUpdatingAttackValues)
            {
                IsUpdatingAttackValues = true;

                JsonDocument activePlayerDoc = null;
                try
                {
                    activePlayerDoc = await GetApiDataWithRetry(ActivePlayerEndpoint, CurrentSettings.ApiRetryCount);
                }
                catch (Exception ex)
                {
                    LogMessage($"获取玩家数据失败: {ex.Message}", LogLevel.Error);
                    IsUpdatingAttackValues = false;
                    return;
                }

                if (activePlayerDoc == null)
                {
                    IsUpdatingAttackValues = false;
                    return;
                }

                if (string.IsNullOrEmpty(ChampionName))
                {
                    ActivePlayerName = activePlayerDoc.RootElement.GetProperty("summonerName").GetString();
                    IsIntializingValues = true;
                    JsonDocument playerListDoc = await GetApiDataWithRetry(PlayerListEndpoint, CurrentSettings.ApiRetryCount);
                    if (playerListDoc == null)
                    {
                        IsIntializingValues = false;
                        IsUpdatingAttackValues = false;
                        return;
                    }
                    foreach (var element in playerListDoc.RootElement.EnumerateArray())
                    {
                        if (element.GetProperty("summonerName").GetString().Equals(ActivePlayerName))
                        {
                            ChampionName = element.GetProperty("championName").GetString();
                            string[] rawNameArray = element.GetProperty("rawChampionName").GetString().Split('_', StringSplitOptions.RemoveEmptyEntries);
                            RawChampionName = rawNameArray[^1];
                        }
                    }

                    if (!await GetChampionBaseValues(RawChampionName))
                    {
                        IsIntializingValues = false;
                        IsUpdatingAttackValues = false;
                        return;
                    }

                    LogMessage($"初始化英雄数据: {ChampionName} (玩家: {ActivePlayerName})", LogLevel.Info);
#if DEBUG
                    Console.Title = $"({ActivePlayerName}) {ChampionName}";
#endif

                    IsIntializingValues = false;
                }

#if DEBUG
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"{owStopWatch.ElapsedMilliseconds}\n" +
                    $"Attack Speed Ratio: {ChampionAttackSpeedRatio}\n" +
                    $"Windup Percent: {ChampionAttackDelayPercent}\n" +
                    $"Current AS: {ClientAttackSpeed:0.00####}\n" +
                    $"Seconds Per Attack: {GetSecondsPerAttack():0.00####}\n" +
                    $"Windup Duration: {GetWindupDuration():0.00####}s + {WindupBuffer}s delay\n" +
                    $"Attack Down Time: {(GetSecondsPerAttack() - GetWindupDuration()):0.00####}s");
#endif

                ClientAttackSpeed = activePlayerDoc.RootElement.GetProperty("championStats").GetProperty("attackSpeed").GetDouble();
                
                // 如果启用自适应且攻速改变，重新设置定时器
                if (CurrentSettings.TimerIntervalMs < 0)
                {
                    var newInterval = GetAdaptiveTimerInterval();
                    if (Math.Abs(OrbWalkTimer.Interval - newInterval) > 1)
                    {
                        OrbWalkTimer.Interval = newInterval;
                        LogMessage($"自适应调整定时器: {newInterval:F2}ms", LogLevel.Info);
                    }
                }
                
                IsUpdatingAttackValues = false;
            }
        }

        private static async Task<bool> GetChampionBaseValues(string championName)
        {
            string lowerChampionName = championName.ToLower();
            JsonDocument championBinDoc = null;
            try
            {
                championBinDoc = await GetApiDataWithRetry($"{ChampionStatsEndpoint}{lowerChampionName}/{lowerChampionName}.bin.json", CurrentSettings.ApiRetryCount);
                if (championBinDoc == null)
                {
                    LogMessage($"获取英雄 {championName} 数据失败", LogLevel.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"获取英雄 {championName} 数据异常: {ex.Message}", LogLevel.Error);
                return false;
            }
            var championRootStats = championBinDoc.RootElement.GetProperty($"Characters/{championName}/CharacterRecords/Root");
            ChampionAttackSpeedRatio = championRootStats.GetProperty("attackSpeedRatio").GetDouble();

            var championBasicAttackInfo = championRootStats.GetProperty("basicAttack");
            var championAttackDelayOffsetToken = championBasicAttackInfo.TryGetProperty("mAttackDelayCastOffsetPercent", out var attackDelayOffset) ? attackDelayOffset : (JsonElement?)null;
            var championAttackDelayOffsetSpeedRatioToken = championBasicAttackInfo.TryGetProperty("mAttackDelayCastOffsetPercentAttackSpeedRatio", out var attackDelayOffsetSpeedRatio) ? attackDelayOffsetSpeedRatio : (JsonElement?)null;

            if (championAttackDelayOffsetSpeedRatioToken.HasValue)
            {
                ChampionAttackDelayScaling = championAttackDelayOffsetSpeedRatioToken.Value.GetDouble();
            }

            if (!championAttackDelayOffsetToken.HasValue)
            {
                var attackTotalTimeToken = championBasicAttackInfo.TryGetProperty("mAttackTotalTime", out var attackTotalTime) ? attackTotalTime : (JsonElement?)null;
                var attackCastTimeToken = championBasicAttackInfo.TryGetProperty("mAttackCastTime", out var attackCastTime) ? attackCastTime : (JsonElement?)null;

                if (!attackTotalTimeToken.HasValue && !attackCastTimeToken.HasValue)
                {
                    string attackName = championBasicAttackInfo.GetProperty("mAttackName").GetString();
                    string attackSpell = $"Characters/{attackName.Split(new[] { "BasicAttack" }, StringSplitOptions.RemoveEmptyEntries)[0]}/Spells/{attackName}";
                    ChampionAttackDelayPercent += championBinDoc.RootElement.GetProperty(attackSpell).GetProperty("mSpell").GetProperty("delayCastOffsetPercent").GetDouble();
                }
                else
                {
                    ChampionAttackTotalTime = attackTotalTimeToken.Value.GetDouble();
                    ChampionAttackCastTime = attackCastTimeToken.Value.GetDouble();

                    ChampionAttackDelayPercent = ChampionAttackCastTime / ChampionAttackTotalTime;
                }
            }
            else
            {
                ChampionAttackDelayPercent += championAttackDelayOffsetToken.Value.GetDouble();
            }

            return true;
        }
    }
}
