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

        public static void ExecuteAttackCommand(DateTime currentTime)
        {
            // 设置输入冷却时间，防止过于频繁的输入
            nextInput = currentTime.AddSeconds(AttackTimingCalculator.GetMinInputDelay());

            // 发送攻击输入：A键 + 左键点击
            InputSimulator.Keyboard.KeyDown((ushort)DirectInputKeys.DIK_A);
            InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Left);
            InputSimulator.Keyboard.KeyUp((ushort)DirectInputKeys.DIK_A);

            // 获取攻击执行后的时间（考虑输入延迟）
            var attackTime = DateTime.Now;

            // 设置下次攻击和移动的时间
            // nextMove: 等待攻击前摇+buffer时间后才能移动，避免取消攻击
            nextMove = attackTime.AddSeconds(AttackTimingCalculator.GetBufferedWindupDuration());
            // nextAttack: 等待攻击间隔时间后才能下次攻击
            nextAttack = attackTime.AddSeconds(AttackTimingCalculator.GetSecondsPerAttack());
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

        public static bool CanMove(DateTime currentTime)
        {
            return nextMove < currentTime;
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
