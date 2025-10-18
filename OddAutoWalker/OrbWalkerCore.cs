using System;
using System.Timers;

namespace OddAutoWalker
{
    /// <summary>
    /// 走A控制器
    /// 负责协调走A的各个组件，管理定时器和执行逻辑
    /// </summary>
    public static class OrbWalkerCore
    {
        // 走A定时器：控制走A的执行频率
        // 固定间隔：33.33ms (30Hz)，确保足够的检查频率
        // 注意：当前版本使用固定间隔，未实现基于攻速的动态调整
        private static Timer OrbWalkTimer = new Timer(100d / 3d);
        private static bool OrbWalkerActive = false;          // 走A是否激活
        private static Settings currentSettings;
        private static double lastMoveTime = 0;               // 上次移动时间   

#if DEBUG
        private static readonly System.Diagnostics.Stopwatch owStopWatch = new System.Diagnostics.Stopwatch();
#endif

        public static void Initialize(Settings settings)
        {
            currentSettings = settings;
            OrbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
            
            // 注意：当前版本使用固定间隔，未实现动态调整
            // 定时器间隔已在声明时设置为 100d/3d (33.33ms)
        }

        public static void ActivateOrbWalker(Settings settings)
        {
            // 防止重复激活：只有在未激活且定时器未运行时才启动
            if (!OrbWalkerActive && !OrbWalkTimer.Enabled)
            {
                OrbWalkerActive = true;
                
                // 按下按键时立即检查是否可以攻击，消除定时器启动的延迟
                // 如果当前可以攻击，立即执行攻击，然后启动定时器完成走A周期
                var currentTime = DateTime.Now;
                if (InputController.CanAttack(currentTime) && 
                    GameStateManager.HasLeagueProcess() && 
                    InputController.IsLeagueWindowActive(GameStateManager.GetLeagueProcess()))
                {
                    // 立即执行攻击
                    InputController.ExecuteAttackCommand(currentTime);
                }
                
                OrbWalkTimer.Start();
            }
        }

        public static void DeactivateOrbWalker()
        {
            OrbWalkerActive = false;
            OrbWalkTimer.Stop();
        }

        public static bool IsOrbWalkerActive() => OrbWalkerActive;

        /// <summary>
        /// 基于攻速计算移动间隔
        /// 使用线性插值算法，在低攻速和高攻速之间平滑过渡
        /// </summary>
        private static double CalculateMoveInterval(double attackSpeed)
        {
            const double minInterval = 33.33;  // 最小间隔 33.33ms (30Hz) - 高攻速时使用
            const double maxInterval = 100.0; // 最大间隔 100ms (10Hz) - 低攻速时使用
            
            if (attackSpeed >= currentSettings.HighAttackSpeedThreshold)
            {
                return minInterval;
            }
            else if (attackSpeed <= currentSettings.LowAttackSpeedThreshold)
            {
                return maxInterval;
            }
            else
            {
                // 线性插值：攻速从LowAttackSpeedThreshold到HighAttackSpeedThreshold
                // 间隔从maxInterval到minInterval
                double ratio = (attackSpeed - currentSettings.LowAttackSpeedThreshold) / 
                              (currentSettings.HighAttackSpeedThreshold - currentSettings.LowAttackSpeedThreshold);
                return maxInterval - ratio * (maxInterval - minInterval);
            }
        }



        private static void OrbWalkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            owStopWatch.Start();
#endif
            if (!GameStateManager.HasLeagueProcess() || 
                GameStateManager.IsExitingProcess() || 
                !InputController.IsLeagueWindowActive(GameStateManager.GetLeagueProcess()))
            {
#if DEBUG
                owStopWatch.Reset();
#endif
                return;
            }

            // Store time at timer tick start into a variable for readability
            var time = e.SignalTime;

            // 检查是否可以发送输入
            if (InputController.CanSendInput(time))
            {
                // 能攻击就攻击
                if (InputController.CanAttack(time))
                {
                    InputController.ExecuteAttackCommand(time);
                }
                // 不能攻击就移动
                else if (InputController.CanMove(time))
                {
                    // 基于攻速的线性插值移动频率控制
                    var attackSpeed = 1.0 / AttackTimingCalculator.GetSecondsPerAttack();
                    var currentTimeMs = time.Ticks / 10000.0; // 转换为毫秒
                    var moveInterval = CalculateMoveInterval(attackSpeed);
                    
                    // 检查是否到了移动时间
                    if (currentTimeMs - lastMoveTime >= moveInterval)
                    {
                        InputController.ExecuteMoveCommand(time);
                        lastMoveTime = currentTimeMs;
                    }
                }
            }
#if DEBUG
            owStopWatch.Reset();
#endif
        }

#if DEBUG
        public static string GetDebugInfo(Settings settings)
        {
            var currentTime = DateTime.Now;
            var attackSpeed = 1.0 / AttackTimingCalculator.GetSecondsPerAttack();
            var windupDuration = AttackTimingCalculator.GetWindupDuration();
            var bufferedWindup = AttackTimingCalculator.GetBufferedWindupDuration();
            
            return $"Player: {GameStateManager.GetActivePlayerName()} | Champion: {GameStateManager.GetChampionName()}\n" +
                   $"AS: {attackSpeed:F2} | SPA: {AttackTimingCalculator.GetSecondsPerAttack():F4}s | Windup: {windupDuration:F4}s | Buffered: {bufferedWindup:F4}s\n" +
                   $"Tick: {OrbWalkTimer.Interval:F1}ms ({1000.0 / OrbWalkTimer.Interval:F1}Hz) | MinInputDelay: {AttackTimingCalculator.GetMinInputDelay() * 1000:F1}ms\n" +
                   $"Active: {OrbWalkerActive} | Timer Running: {OrbWalkTimer.Enabled} | API Calls: {GameStateManager.GetApiCallCount()} | Latency: {GameStateManager.GetApiLatency():F1}ms\n" +
                   $"Move Commands: {InputController.GetMoveCommandCount()}/5s | CanAttack: {InputController.CanAttack(currentTime)} | CanMove: {InputController.CanMove(currentTime)}";
        }
#endif
    }
}


