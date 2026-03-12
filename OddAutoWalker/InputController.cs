using System;
using System.Runtime.InteropServices;

namespace OddAutoWalker
{
    /// <summary>
    /// 输入控制器
    /// 负责管理走A的输入操作和时间控制
    /// </summary>
    public static class InputController
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // 走A执行时间控制
        private static DateTime nextInput = default;               // 下次可以发送输入的时间
        private static DateTime nextMove = default;                // 下次可以移动的时间
        private static DateTime nextAttack = default;              // 下次可以攻击的时间

        // 移动指令控制
        private static DateTime lastMoveTime = DateTime.Now;       // 上次移动指令时间
        private static int moveCommandCount = 0;                   // 移动指令计数器（用于调试）
        private static DateTime lastMoveCountReset = DateTime.Now; // 移动计数器重置时间

        // 线性频率控制
        private static double lowAttackSpeedThreshold = AttackTimingCalculator.DEFAULT_LOW_AS_THRESHOLD;
        private static double highAttackSpeedThreshold = AttackTimingCalculator.DEFAULT_HIGH_AS_THRESHOLD;

        public static void ExecuteAttackCommand(DateTime currentTime)
        {
            // 发送攻击输入：A键 + 左键点击
            InputSimulator.Keyboard.KeyDown((ushort)DirectInputKeys.DIK_A);
            InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Left);
            InputSimulator.Keyboard.KeyUp((ushort)DirectInputKeys.DIK_A);

            // 在输入发送之后记录时间（游戏此时应已收到输入，开始计算前摇）
            var attackTime = DateTime.Now;

            // 计算动态安全边际
            var windupDuration = AttackTimingCalculator.GetWindupDuration();
            var dynamicBuffer = CalculateDynamicBuffer(windupDuration);

            // 设置下次移动时间（前摇 + 动态缓冲 + 输入延迟补偿）
            nextMove = attackTime.AddSeconds(windupDuration + dynamicBuffer + AttackTimingCalculator.INPUT_LATENCY_COMPENSATION);
            // 设置下次攻击时间
            nextAttack = attackTime.AddSeconds(AttackTimingCalculator.GetSecondsPerAttack());
            // 设置输入冷却时间
            nextInput = attackTime.AddSeconds(AttackTimingCalculator.GetMinInputDelay());
            // 重置移动时间，防止攻击后立即移动
            lastMoveTime = attackTime;
        }

        public static void ExecuteMoveCommand(DateTime currentTime)
        {
            // 设置输入冷却时间，防止过于频繁的输入
            nextInput = currentTime.AddSeconds(AttackTimingCalculator.GetMinInputDelay());

            // 发送移动输入：右键点击
            InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Right);
            
            // 更新最后移动时间和计数器
            lastMoveTime = currentTime;
            moveCommandCount++;
        }

        public static bool CanAttack(DateTime currentTime)
        {
            return nextAttack < currentTime;
        }

        /// <summary>
        /// 检查是否可以移动（集成线性频率控制）
        /// </summary>
        public static bool CanMove(DateTime currentTime)
        {
            if (nextMove >= currentTime)
            {
                return false;
            }

            // 基于攻速的线性频率控制
            var attackSpeed = 1.0 / AttackTimingCalculator.GetSecondsPerAttack();
            var moveInterval = CalculateMoveInterval(attackSpeed);
            var timeSinceLastMove = (currentTime - lastMoveTime).TotalMilliseconds;

            return timeSinceLastMove >= moveInterval;
        }

        /// <summary>
        /// 基于攻速计算移动间隔（线性插值）
        /// </summary>
        public static double CalculateMoveInterval(double attackSpeed)
        {
            if (attackSpeed >= highAttackSpeedThreshold)
            {
                return AttackTimingCalculator.MIN_MOVE_INTERVAL_MS;
            }
            else if (attackSpeed <= lowAttackSpeedThreshold)
            {
                return AttackTimingCalculator.MAX_MOVE_INTERVAL_MS;
            }
            else
            {
                // 线性插值
                double ratio = (attackSpeed - lowAttackSpeedThreshold) /
                              (highAttackSpeedThreshold - lowAttackSpeedThreshold);
                return AttackTimingCalculator.MAX_MOVE_INTERVAL_MS - ratio * (AttackTimingCalculator.MAX_MOVE_INTERVAL_MS - AttackTimingCalculator.MIN_MOVE_INTERVAL_MS);
            }
        }

        /// <summary>
        /// 计算动态缓冲时间：前摇越短，缓冲比例越高
        /// </summary>
        public static double CalculateDynamicBuffer(double windupDuration)
        {
            // 取基础缓冲和比例缓冲中的较大值
            var ratioBuffer = windupDuration * AttackTimingCalculator.MIN_BUFFER_RATIO;
            return Math.Max(AttackTimingCalculator.WINDUP_BUFFER, ratioBuffer);
        }

        /// <summary>
        /// 设置攻速阈值
        /// </summary>
        public static void SetAttackSpeedThresholds(double low, double high)
        {
            lowAttackSpeedThreshold = low;
            highAttackSpeedThreshold = high;
        }

        public static bool CanSendInput(DateTime currentTime)
        {
            return nextInput < currentTime;
        }


        public static bool IsLeagueWindowActive(System.Diagnostics.Process leagueProcess)
        {
            return GetForegroundWindow() == leagueProcess.MainWindowHandle;
        }

        public static int GetMoveCommandCount() => moveCommandCount;

        public static void ResetMoveCommandCount()
        {
            moveCommandCount = 0;
            lastMoveCountReset = DateTime.Now;
        }

#if DEBUG
        public static string GetDebugInfo()
        {
            var currentTime = DateTime.Now;
            return $"NextInput: {(nextInput - currentTime).TotalMilliseconds:F1}ms | " +
                   $"NextMove: {(nextMove - currentTime).TotalMilliseconds:F1}ms | " +
                   $"NextAttack: {(nextAttack - currentTime).TotalMilliseconds:F1}ms | " +
                   $"LastMove: {(currentTime - lastMoveTime).TotalMilliseconds:F1}ms ago";
        }
#endif
    }
}
