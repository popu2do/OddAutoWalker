using System;
using System.Timers;

namespace OddAutoWalker
{
    /// <summary>
    /// 走A核心控制器
    /// 负责协调走A的各个组件，管理定时器和执行逻辑
    /// </summary>
    public static class OrbWalkerCore
    {
        // 走A定时器：控制走A的执行频率
        // Interval 动态调整：基于攻速和 MaxCheckFrequencyHz 计算
        // 目标：每次攻击周期内进行8-12次检查，确保精确度
        private static Timer OrbWalkTimer = new Timer(100d / 3d);
        private static bool OrbWalkerTimerActive = false;          // 走A定时器是否激活

        // 攻速变化检测
        private static double LastAttackSpeed = 0.625;             // 上次攻击速度，用于检测变化
        
        // 设置引用
        private static Settings currentSettings = null;

#if DEBUG
        private static int TimerCallbackCounter = 0;
        private static readonly System.Diagnostics.Stopwatch owStopWatch = new System.Diagnostics.Stopwatch();
#endif

        /// <summary>
        /// 初始化走A核心
        /// </summary>
        /// <param name="settings">设置对象</param>
        public static void Initialize(Settings settings)
        {
            currentSettings = settings;
            OrbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
            
            // 初始化计算刻间隔
            UpdateTickInterval(settings);
        }

        /// <summary>
        /// 启动走A定时器
        /// </summary>
        /// <param name="settings">设置对象</param>
        public static void StartOrbWalker(Settings settings)
        {
            if (!OrbWalkerTimerActive)
            {
                OrbWalkerTimerActive = true;
                
                // 按下按键时立即检查是否可以攻击，消除定时器启动的垃圾时间
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

        /// <summary>
        /// 停止走A定时器
        /// </summary>
        public static void StopOrbWalker()
        {
            // 点按处理：松开按键时不立即停止定时器
            // 让定时器继续运行，直到完成当前的走A周期（移动指令发送）
            // 这样可以确保点按也能获得完整的走A体验
            OrbWalkerTimerActive = false;
            // 不立即停止定时器，让移动指令有机会发送
        }

        /// <summary>
        /// 检查走A定时器是否激活
        /// </summary>
        /// <returns>定时器是否激活</returns>
        public static bool IsOrbWalkerActive() => OrbWalkerTimerActive;

        /// <summary>
        /// 更新计算刻间隔
        /// </summary>
        /// <param name="settings">设置对象</param>
        public static void UpdateTickInterval(Settings settings)
        {
            if (OrbWalkTimer != null)
            {
                var newInterval = AttackTimingCalculator.GetTickInterval(settings);
                OrbWalkTimer.Interval = newInterval;
            }
        }

        /// <summary>
        /// 检查攻速变化并更新定时器间隔
        /// </summary>
        /// <param name="currentAttackSpeed">当前攻击速度</param>
        /// <param name="settings">设置对象</param>
        public static void CheckAttackSpeedChange(double currentAttackSpeed, Settings settings)
        {
            // 检测攻速变化，如果变化超过阈值则更新计算刻间隔
            if (Math.Abs(currentAttackSpeed - LastAttackSpeed) > 0.01) // 0.01的阈值避免微小变化
            {
                UpdateTickInterval(settings);
                LastAttackSpeed = currentAttackSpeed;
            }
        }

        /// <summary>
        /// 走A定时器回调函数
        /// 
        /// 走A = 攻击 + 移动的循环，关键在于时机控制：
        /// 1. 攻击时机：等待攻击冷却时间结束
        /// 2. 移动时机：等待攻击前摇+buffer时间结束
        /// 3. 输入控制：防止过于频繁的输入导致系统负载
        /// 
        /// 前摇/后摇概念：
        /// - 前摇（Windup）：攻击动画开始到伤害产生的时间，期间移动会取消攻击
        /// - 后摇（Follow-through）：伤害产生后的动画时间，期间可以自由移动
        /// - Buffer：额外缓冲时间，防止因FPS/网络延迟过早移动
        /// 
        /// 执行优先级：
        /// 1. 优先攻击：如果可以攻击，立即执行攻击
        /// 2. 其次移动：如果攻击冷却中但可以移动，执行移动
        /// 3. 等待时机：如果都不满足，等待下次计算刻
        /// 
        /// 处理方式：
        /// - 减少不必要的移动指令，避免无效微操
        /// - 走A移动间隔，根据攻速动态调整
        /// - 输入冷却控制，防止系统过载
        /// 
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">定时器事件参数</param>
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

            // 走A处理：减少不必要的移动指令
            if (InputController.CanSendInput(time))
            {
                // 优先攻击
                if (InputController.CanAttack(time))
                {
                    // 使用统一的攻击执行方法
                    InputController.ExecuteAttackCommand(time);
                }
                // 移动处理：添加冷却时间和走A判断
                else if (InputController.CanMove(time))
                {
                    // 使用存储的设置对象
                    if (currentSettings != null && InputController.ShouldSendMoveCommand(time, currentSettings))
                    {
                        InputController.ExecuteMoveCommand(time);
                        
                        // 点按处理：如果定时器已停用且移动指令已发送，停止定时器
                        // 这样确保点按也能完成完整的走A周期
                        if (!OrbWalkerTimerActive)
                        {
                            OrbWalkTimer.Stop();
                        }
                    }
                }
            }
#if DEBUG
            owStopWatch.Reset();
#endif
        }

#if DEBUG
        /// <summary>
        /// 获取调试信息
        /// </summary>
        /// <returns>调试信息字符串</returns>
        public static string GetDebugInfo(Settings settings)
        {
            return $"Stopwatch: {owStopWatch.ElapsedMilliseconds}ms\n" +
                   $"Player: {GameStateManager.GetActivePlayerName()} | Champion: {GameStateManager.GetChampionName()}\n" +
                   $"AS: {AttackTimingCalculator.GetSecondsPerAttack():F4} | SPA: {AttackTimingCalculator.GetSecondsPerAttack():F4}s | Windup: {AttackTimingCalculator.GetWindupDuration():F4}s\n" +
                   $"Tick: {OrbWalkTimer.Interval:F1}ms ({1000.0 / OrbWalkTimer.Interval:F1}Hz) | Move: {AttackTimingCalculator.GetOrbWalkMoveInterval(settings) * 1000:F1}ms\n" +
                   $"Active: {OrbWalkerTimerActive} | API Calls: {GameStateManager.GetApiCallCount()} | Latency: {GameStateManager.GetApiLatency():F1}ms\n" +
                   $"Move Commands: {InputController.GetMoveCommandCount()}/5s";
        }
#endif
    }
}
