using LowLevelInput.Hooks;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Timers;

namespace OddAutoWalker
{
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
        private static readonly WebClient Client = new WebClient();
        private static readonly InputManager InputManager = new InputManager();
        private static Process LeagueProcess = null;

        private static Timer OrbWalkTimer = new Timer(100d / 3d);

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
        
        // 用于检测攻速变化
        private static double LastAttackSpeed = 0.625;
        
        // API状态信息
        private static int apiCallCount = 0;
        private static DateTime lastApiCall = DateTime.Now;
        private static double apiLatency = 0;
        

        /// <summary>
        /// This is a buffer to prevent you from accidentally canceling your auto-attack too soon, as a result of fps, ping, or otherwise.
        /// </summary>
        private static readonly double WindupBuffer = 1d / 15d;

        // If we're trying to input faster than this, don't
        private static readonly double MinInputDelay = 1d / 30d;

        // This is honestly just semi-random because we need an interval to run the timer at
        private static readonly double OrderTickRate = 1d / 30d;

#if DEBUG
        private static int TimerCallbackCounter = 0;
#endif

        // These are all in seconds
        public static double GetSecondsPerAttack() => 1 / ClientAttackSpeed;
        public static double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;
        public static double GetBufferedWindupDuration() => GetWindupDuration() + WindupBuffer;

        /// <summary>
        /// 计算计算刻间隔（Tick Interval）
        /// 自适应，基于系统性能和攻速动态调整
        /// </summary>
        /// <returns>计算刻间隔（毫秒）</returns>
        public static double GetTickInterval()
        {
            var secondsPerAttack = GetSecondsPerAttack();
            
            // 目标：每次攻击周期内进行8-12次检查，确保精确度
            var idealChecksPerAttack = 10; // 每次攻击检查10次
            var idealIntervalMs = (secondsPerAttack / idealChecksPerAttack) * 1000;
            
            // 上限：受 MaxCheckFrequencyHz 限制，防止CPU占用过高
            // 例如：120Hz = 1000ms / 120 = 8.33ms
            var maxFrequencyHz = CurrentSettings.MaxCheckFrequencyHz;
            var minIntervalMs = 1000.0 / maxFrequencyHz;
            
            // 下限：根据攻速动态调整，攻速越高，检查间隔越短
            // 但不能超过最大频率限制
            var tickIntervalMs = Math.Max(idealIntervalMs, minIntervalMs);
            
            return tickIntervalMs;
        }

        /// <summary>
        /// 更新计算刻间隔
        /// </summary>
        public static void UpdateTickInterval()
        {
            if (OrbWalkTimer != null)
            {
                var newInterval = GetTickInterval();
                OrbWalkTimer.Interval = newInterval;
            }
        }


        /// <summary>
        /// 计算智能移动间隔
        /// 基于攻速智能调整，受 min/max 限制
        /// </summary>
        /// <returns>移动间隔（秒）</returns>
        private static double GetSmartMoveInterval()
        {
            var secondsPerAttack = GetSecondsPerAttack();
            
            // 曲线算法：移动间隔与攻击间隔的关系
            // 目标：攻击间隔越长，移动间隔也越长，但增长幅度递减
            
            // 1. 基础移动间隔：攻击间隔的30-50%
            // 使用对数曲线，让移动间隔随攻击间隔增长但增长幅度递减
            var baseRatio = 0.3 + (0.2 * Math.Log(1 + secondsPerAttack) / Math.Log(2)); // 0.3-0.5范围
            
            // 2. 计算移动间隔
            var moveInterval = secondsPerAttack * baseRatio;
            
            // 3. 限制范围：确保在 min/max 之间
            var minInterval = CurrentSettings.MinMoveCommandIntervalSeconds;  // 防止过于频繁导致掉线
            var maxInterval = CurrentSettings.MaxMoveCommandIntervalSeconds;  // 防止间隔过长影响走位
            
            return Math.Max(Math.Min(moveInterval, maxInterval), minInterval);
        }

        /// <summary>
        /// 判断是否应该发送移动指令
        /// 检查是否满足计算刻时机和移动间隔要求
        /// </summary>
        private static bool ShouldSendMoveCommand(DateTime currentTime)
        {
            // 使用智能移动逻辑计算移动间隔
            var moveInterval = GetSmartMoveInterval();
            
            // 检查是否达到移动间隔
            return (currentTime - lastMoveTime).TotalSeconds >= moveInterval;
        }

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

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            Client.Proxy = null;

            Console.Clear();
            Console.CursorVisible = false;

            InputManager.Initialize();
            InputManager.OnKeyboardEvent += InputManager_OnKeyboardEvent;
            InputManager.OnMouseEvent += InputManager_OnMouseEvent;

            OrbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
            
            // 初始化计算刻间隔
            UpdateTickInterval();
            
#if DEBUG
            Timer callbackTimer = new Timer(16.66);
            callbackTimer.Elapsed += Timer_CallbackLog;
#endif

            Timer attackSpeedCacheTimer = new Timer(OrderTickRate);
            attackSpeedCacheTimer.Elapsed += AttackSpeedCacheTimer_Elapsed;

            attackSpeedCacheTimer.Start();
            Console.WriteLine($"Press and hold '{(VirtualKeyCode)CurrentSettings.ActivationKey}' to activate the Orb Walker");

            CheckLeagueProcess();

            Console.ReadLine();
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
#endif

        private static void InputManager_OnMouseEvent(VirtualKeyCode key, KeyState state, int x, int y)
        {
        }

        private static void InputManager_OnKeyboardEvent(VirtualKeyCode key, KeyState state)
        {
            if (key == (VirtualKeyCode)CurrentSettings.ActivationKey)
            {
                switch (state)
                {
                    case KeyState.Down when !OrbWalkerTimerActive:
                        OrbWalkerTimerActive = true;
                        OrbWalkTimer.Start();
                        break;

                    case KeyState.Up when OrbWalkerTimerActive:
                        OrbWalkerTimerActive = false;
                        OrbWalkTimer.Stop();
                        break;
                }
            }
        }

        // When these DateTime instances are in the past, the action they gate can be taken
        private static DateTime nextInput = default;
        private static DateTime nextMove = default;
        private static DateTime nextAttack = default;
        
        // 移动指令冷却时间，防止过于频繁的移动指令
        private static DateTime lastMoveTime = default;
        
        // 移动指令计数器，用于调试和限制
        private static int moveCommandCount = 0;
        private static DateTime lastMoveCountReset = DateTime.Now;

        private static readonly Stopwatch owStopWatch = new Stopwatch();

        private static void OrbWalkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            owStopWatch.Start();
#endif
            if (!HasProcess || IsExiting || GetForegroundWindow() != LeagueProcess.MainWindowHandle)
            {
#if DEBUG
                owStopWatch.Reset();
#endif
                return;
            }

            // Store time at timer tick start into a variable for readability
            var time = e.SignalTime;

            // 优化后的走A逻辑：减少不必要的移动指令
            if (nextInput < time)
            {
                // 优先攻击
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
                    // 等待攻击完成才能移动，避免取消攻击
                    nextMove = attackTime.AddSeconds(GetBufferedWindupDuration()); // 前摇+buffer时间
                    nextAttack = attackTime.AddSeconds(GetSecondsPerAttack());
                    
                    // 不立即更新lastMoveTime，让移动逻辑自然检查间隔
                    // lastMoveTime 会在实际移动时更新
                }
                // 移动逻辑优化：添加冷却时间和智能判断
                else if (nextMove < time && ShouldSendMoveCommand(time))
                {
                    // Store current time + input delay so we're aware when we can attack / move next
                    nextInput = time.AddSeconds(MinInputDelay);

                    // Send move input
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Right);
                    
                    // 更新最后移动时间和计数器
                    lastMoveTime = time;
                    moveCommandCount++;
                    
                    // 每5秒重置计数器
                    if ((time - lastMoveCountReset).TotalSeconds >= 5)
                    {
                        lastMoveCountReset = time;
                        moveCommandCount = 0;
                    }
                }
            }
#if DEBUG
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
            //Console.Clear();
            Console.WriteLine("League Process Exited");
            CheckLeagueProcess();
        }

        private static void AttackSpeedCacheTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (HasProcess && !IsExiting && !IsIntializingValues && !IsUpdatingAttackValues)
            {
                IsUpdatingAttackValues = true;

                JToken activePlayerToken = null;
                try
                {
                    var apiStartTime = DateTime.Now;
                    activePlayerToken = JToken.Parse(Client.DownloadString(ActivePlayerEndpoint));
                    apiLatency = (DateTime.Now - apiStartTime).TotalMilliseconds;
                    apiCallCount++;
                    lastApiCall = DateTime.Now;
                }
                catch
                {
                    IsUpdatingAttackValues = false;
                    return;
                }

                if (string.IsNullOrEmpty(ChampionName))
                {
                    ActivePlayerName = activePlayerToken?["summonerName"].ToString();
                    IsIntializingValues = true;
                    JToken playerListToken = JToken.Parse(Client.DownloadString(PlayerListEndpoint));
                    foreach (JToken token in playerListToken)
                    {
                        if (token["summonerName"].ToString().Equals(ActivePlayerName))
                        {
                            ChampionName = token["championName"].ToString();
                            string[] rawNameArray = token["rawChampionName"].ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
                            RawChampionName = rawNameArray[^1];
                        }
                    }

                    if (!GetChampionBaseValues(RawChampionName))
                    {
                        IsIntializingValues = false;
                        IsUpdatingAttackValues = false;
                        return;
                    }

#if DEBUG
                    Console.Title = $"({ActivePlayerName}) {ChampionName}";
#endif

                    IsIntializingValues = false;
                }

#if DEBUG
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Stopwatch: {owStopWatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"Player: {ActivePlayerName} | Champion: {ChampionName}");
                Console.WriteLine($"AS: {ClientAttackSpeed:F4} | SPA: {GetSecondsPerAttack():F4}s | Windup: {GetWindupDuration():F4}s");
                Console.WriteLine($"Tick: {OrbWalkTimer.Interval:F1}ms ({1000.0 / OrbWalkTimer.Interval:F1}Hz) | Move: {GetSmartMoveInterval() * 1000:F1}ms");
                Console.WriteLine($"Active: {OrbWalkerTimerActive} | API Calls: {apiCallCount} | Latency: {apiLatency:F1}ms");
                Console.WriteLine($"Move Commands: {moveCommandCount}/5s");
#endif

                ClientAttackSpeed = activePlayerToken["championStats"]["attackSpeed"].Value<double>();
                
                // 检测攻速变化，如果变化超过阈值则更新计算刻间隔
                if (Math.Abs(ClientAttackSpeed - LastAttackSpeed) > 0.01) // 0.01的阈值避免微小变化
                {
                    UpdateTickInterval();
                    LastAttackSpeed = ClientAttackSpeed;
                }
                
                IsUpdatingAttackValues = false;
            }
        }

        private static bool GetChampionBaseValues(string championName)
        {
            string lowerChampionName = championName.ToLower();
            JToken championBinToken = null;
            try
            {
                championBinToken = JToken.Parse(Client.DownloadString($"{ChampionStatsEndpoint}{lowerChampionName}/{lowerChampionName}.bin.json"));
            }
            catch
            {
                return false;
            }
            JToken championRootStats = championBinToken[$"Characters/{championName}/CharacterRecords/Root"];
            ChampionAttackSpeedRatio = championRootStats["attackSpeedRatio"].Value<double>(); ;

            JToken championBasicAttackInfoToken = championRootStats["basicAttack"];
            JToken championAttackDelayOffsetToken = championBasicAttackInfoToken["mAttackDelayCastOffsetPercent"];
            JToken championAttackDelayOffsetSpeedRatioToken = championBasicAttackInfoToken["mAttackDelayCastOffsetPercentAttackSpeedRatio"];

            if (championAttackDelayOffsetSpeedRatioToken?.Value<double?>() != null)
            {
                ChampionAttackDelayScaling = championAttackDelayOffsetSpeedRatioToken.Value<double>();
            }

            // 始终尝试获取攻击时间数据
            JToken attackTotalTimeToken = championBasicAttackInfoToken["mAttackTotalTime"];
            JToken attackCastTimeToken = championBasicAttackInfoToken["mAttackCastTime"];

            if (attackTotalTimeToken?.Value<double?>() != null && attackCastTimeToken?.Value<double?>() != null)
            {
                // 有完整的攻击时间数据
                ChampionAttackTotalTime = attackTotalTimeToken.Value<double>();
                ChampionAttackCastTime = attackCastTimeToken.Value<double>();
                ChampionAttackDelayPercent = ChampionAttackCastTime / ChampionAttackTotalTime;
            }
            else if (championAttackDelayOffsetToken?.Value<double?>() != null)
            {
                // 使用延迟偏移百分比
                ChampionAttackDelayPercent += championAttackDelayOffsetToken.Value<double>();
            }
            else
            {
                // 尝试从攻击技能获取
                string attackName = championBasicAttackInfoToken["mAttackName"].ToString();
                string attackSpell = $"Characters/{attackName.Split(new[] { "BasicAttack" }, StringSplitOptions.RemoveEmptyEntries)[0]}/Spells/{attackName}";
                ChampionAttackDelayPercent += championBinToken[attackSpell]["mSpell"]["delayCastOffsetPercent"].Value<double>();
            }

            return true;
        }
    }
}
