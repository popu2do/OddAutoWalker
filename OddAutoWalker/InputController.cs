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
        private static DateTime lastMoveTime = default;            // 上次移动指令时间
        private static int moveCommandCount = 0;                   // 移动指令计数器（用于调试）
        private static DateTime lastMoveCountReset = DateTime.Now; // 移动计数器重置时间

        /// <summary>
        /// 执行攻击指令
        /// 包含攻击输入、时间计算和冷却设置
        /// </summary>
        /// <param name="currentTime">当前时间</param>
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

        /// <summary>
        /// 执行移动指令
        /// 包含移动输入、时间计算和冷却设置
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        public static void ExecuteMoveCommand(DateTime currentTime)
        {
            // 设置输入冷却时间，防止过于频繁的输入
            nextInput = currentTime.AddSeconds(AttackTimingCalculator.GetMinInputDelay());

            // 发送移动输入：右键点击
            InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Right);
            
            // 更新最后移动时间和计数器
            lastMoveTime = currentTime;
            moveCommandCount++;
            
            // 每5秒重置计数器
            if ((currentTime - lastMoveCountReset).TotalSeconds >= 5)
            {
                lastMoveCountReset = currentTime;
                moveCommandCount = 0;
            }
        }

        /// <summary>
        /// 判断是否可以攻击
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否可以攻击</returns>
        public static bool CanAttack(DateTime currentTime)
        {
            return nextAttack < currentTime;
        }

        /// <summary>
        /// 判断是否可以移动
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否可以移动</returns>
        public static bool CanMove(DateTime currentTime)
        {
            return nextMove < currentTime;
        }

        /// <summary>
        /// 判断是否可以发送输入
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否可以发送输入</returns>
        public static bool CanSendInput(DateTime currentTime)
        {
            return nextInput < currentTime;
        }

        /// <summary>
        /// 判断是否应该发送移动指令
        /// 
        /// 移动指令不能过于频繁，否则可能导致：
        /// 1. 游戏服务器拒绝指令（掉线风险）
        /// 2. 客户端卡顿或输入延迟
        /// 3. 无效的微操（移动距离太短）
        /// 
        /// 判断方法：
        /// 检查当前时间与上次移动时间的间隔是否达到走A移动间隔
        /// 只有达到间隔要求才能发送新的移动指令
        /// 
        /// 与计算刻的关系：
        /// - 计算刻：检查"是否可以移动"的频率（高频率）
        /// - 移动间隔：实际"发送移动指令"的频率（低频率）
        /// - 关系：移动频率 ≤ 计算刻频率
        /// 
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <param name="settings">设置对象</param>
        /// <returns>是否可以发送移动指令</returns>
        public static bool ShouldSendMoveCommand(DateTime currentTime, Settings settings)
        {
            // 使用走A移动计算移动间隔
            var moveInterval = AttackTimingCalculator.GetOrbWalkMoveInterval(settings);
            
            // 检查是否达到移动间隔
            return (currentTime - lastMoveTime).TotalSeconds >= moveInterval;
        }

        /// <summary>
        /// 检查当前窗口是否为LOL窗口
        /// </summary>
        /// <param name="leagueProcess">LOL进程</param>
        /// <returns>是否为LOL窗口</returns>
        public static bool IsLeagueWindowActive(System.Diagnostics.Process leagueProcess)
        {
            return GetForegroundWindow() == leagueProcess.MainWindowHandle;
        }

        /// <summary>
        /// 获取移动指令计数
        /// </summary>
        /// <returns>移动指令计数</returns>
        public static int GetMoveCommandCount() => moveCommandCount;

        /// <summary>
        /// 重置移动指令计数
        /// </summary>
        public static void ResetMoveCommandCount()
        {
            moveCommandCount = 0;
            lastMoveCountReset = DateTime.Now;
        }
    }
}
