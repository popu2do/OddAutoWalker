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
        // 走A定时器：固定间隔 33.33ms (30Hz)，禁用自动重置防止并发
        private static Timer OrbWalkTimer = new Timer(AttackTimingCalculator.GetTickRateMs()) { AutoReset = false };
        private static bool OrbWalkerActive = false;
        private static Settings currentSettings;

        // 线程安全锁，防止定时器回调竞态条件
        private static readonly object _timerLock = new object();

#if DEBUG
        private static readonly System.Diagnostics.Stopwatch owStopWatch = new System.Diagnostics.Stopwatch();
#endif

        public static void Initialize(Settings settings)
        {
            currentSettings = settings;
            OrbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
            InputController.SetAttackSpeedThresholds(settings.LowAttackSpeedThreshold, settings.HighAttackSpeedThreshold);
        }

        public static void ActivateOrbWalker(Settings settings)
        {
            if (!OrbWalkerActive && !OrbWalkTimer.Enabled)
            {
                OrbWalkerActive = true;

                // 激活时立即尝试执行操作，消除定时器启动延迟
                var currentTime = DateTime.Now;
                if (GameStateManager.HasLeagueProcess() &&
                    InputController.IsLeagueWindowActive(GameStateManager.GetLeagueProcess()) &&
                    InputController.CanSendInput(currentTime))
                {
                    if (InputController.CanAttack(currentTime))
                    {
                        InputController.ExecuteAttackCommand(currentTime);
                    }
                    else if (InputController.CanMove(currentTime))
                    {
                        InputController.ExecuteMoveCommand(currentTime);
                    }
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

        private static void OrbWalkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 使用锁确保状态一致性，防止竞态条件
            lock (_timerLock)
            {
#if DEBUG
                owStopWatch.Start();
#endif
                try
                {
                    if (!GameStateManager.HasLeagueProcess() ||
                        GameStateManager.IsExitingProcess() ||
                        !InputController.IsLeagueWindowActive(GameStateManager.GetLeagueProcess()))
                    {
                        return;
                    }

                    var currentTime = DateTime.Now;

                    if (InputController.CanSendInput(currentTime))
                    {
                        if (InputController.CanAttack(currentTime))
                        {
                            InputController.ExecuteAttackCommand(currentTime);
                        }
                        else if (InputController.CanMove(currentTime))
                        {
                            InputController.ExecuteMoveCommand(currentTime);
                        }
                    }
                }
                finally
                {
#if DEBUG
                    owStopWatch.Reset();
#endif
                    // 手动重启定时器（AutoReset = false）
                    if (OrbWalkerActive)
                    {
                        OrbWalkTimer.Start();
                    }
                }
            }
        }

#if DEBUG
        public static string GetDebugInfo(Settings settings)
        {
            var currentTime = DateTime.Now;
            var attackSpeed = 1.0 / AttackTimingCalculator.GetSecondsPerAttack();
            var windupDuration = AttackTimingCalculator.GetWindupDuration();
            var dynamicBuffer = InputController.CalculateDynamicBuffer(windupDuration);
            var inputLatency = AttackTimingCalculator.INPUT_LATENCY_COMPENSATION;
            var totalWait = windupDuration + dynamicBuffer + inputLatency;
            var moveInterval = InputController.CalculateMoveInterval(attackSpeed);

            return $"Player: {GameStateManager.GetActivePlayerName()} | Champion: {GameStateManager.GetChampionName()}\n" +
                   $"AS: {attackSpeed:F2} | Windup: {windupDuration * 1000:F1}ms | Buffer: {dynamicBuffer * 1000:F1}ms | InputComp: {inputLatency * 1000:F0}ms | Total: {totalWait * 1000:F1}ms\n" +
                   $"MoveInterval: {moveInterval:F1}ms | Tick: {OrbWalkTimer.Interval:F1}ms | MinInput: {AttackTimingCalculator.GetMinInputDelay() * 1000:F1}ms\n" +
                   $"Active: {OrbWalkerActive} | API: {GameStateManager.GetApiCallCount()} calls | Latency: {GameStateManager.GetApiLatency():F1}ms\n" +
                   $"CanAttack: {InputController.CanAttack(currentTime)} | CanMove: {InputController.CanMove(currentTime)} | {InputController.GetDebugInfo()}";
        }
#endif
    }
}


