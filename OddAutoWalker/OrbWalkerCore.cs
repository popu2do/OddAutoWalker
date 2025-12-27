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
        // 走A定时器：固定间隔 33.33ms (30Hz)
        private static Timer OrbWalkTimer = new Timer(AttackTimingCalculator.GetTickRateMs());
        private static bool OrbWalkerActive = false;
        private static Settings currentSettings;   

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
            var dynamicBuffer = InputController.CalculateDynamicBuffer(windupDuration);
            var moveInterval = InputController.CalculateMoveInterval(attackSpeed);

            return $"Player: {GameStateManager.GetActivePlayerName()} | Champion: {GameStateManager.GetChampionName()}\n" +
                   $"AS: {attackSpeed:F2} | Windup: {windupDuration * 1000:F1}ms | Buffer: {dynamicBuffer * 1000:F1}ms | Total: {(windupDuration + dynamicBuffer) * 1000:F1}ms\n" +
                   $"MoveInterval: {moveInterval:F1}ms | Tick: {OrbWalkTimer.Interval:F1}ms | MinInput: {AttackTimingCalculator.GetMinInputDelay() * 1000:F1}ms\n" +
                   $"Active: {OrbWalkerActive} | API: {GameStateManager.GetApiCallCount()} calls | Latency: {GameStateManager.GetApiLatency():F1}ms\n" +
                   $"CanAttack: {InputController.CanAttack(currentTime)} | CanMove: {InputController.CanMove(currentTime)} | {InputController.GetDebugInfo()}";
        }
#endif
    }
}


