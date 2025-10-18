using System;

namespace OddAutoWalker
{
    /// <summary>
    /// 攻击时间计算器
    /// 负责计算攻击相关的各种时间参数
    /// </summary>
    public static class AttackTimingCalculator
    {
        /// <summary>
        /// 前摇缓冲时间（秒）
        /// 防止因FPS、网络延迟等因素过早取消攻击
        /// 设置为 1/15 秒 ≈ 66.7ms，确保攻击不会被打断
        /// </summary>
        private static readonly double WindupBuffer = 1d / 15d;

        /// <summary>
        /// 最小输入延迟（秒）
        /// 防止输入过于频繁导致系统负载过高
        /// 设置为 1/30 秒 ≈ 33.3ms，限制最高30Hz的输入频率
        /// </summary>
        private static readonly double MinInputDelay = 1d / 30d;

        /// <summary>
        /// 指令检查频率（秒）
        /// 用于攻击速度缓存的更新频率
        /// 设置为 1/30 秒 ≈ 33.3ms，30Hz的检查频率
        /// </summary>
        private static readonly double OrderTickRate = 1d / 30d;

        // 攻击速度相关（从游戏API获取）
        private static double ClientAttackSpeed = 0.625;           // 客户端攻击速度（次/秒）
        private static double ChampionAttackCastTime = 0.625;      // 英雄攻击施法时间（前摇）
        private static double ChampionAttackTotalTime = 0.625;     // 英雄攻击总时间
        private static double ChampionAttackSpeedRatio = 0.625;    // 英雄攻击速度比例
        private static double ChampionAttackDelayPercent = 0.3;    // 攻击延迟百分比
        private static double ChampionAttackDelayScaling = 1.0;    // 攻击延迟缩放系数

        public static double GetSecondsPerAttack() => 1 / ClientAttackSpeed;
        public static double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;
        public static double GetBufferedWindupDuration() => GetWindupDuration() + WindupBuffer;

        public static void UpdateAttackSpeed(double attackSpeed)
        {
            ClientAttackSpeed = attackSpeed;
        }

        public static void UpdateChampionAttackData(double castTime, double totalTime, double speedRatio, double delayPercent, double delayScaling)
        {
            ChampionAttackCastTime = castTime;
            ChampionAttackTotalTime = totalTime;
            ChampionAttackSpeedRatio = speedRatio;
            ChampionAttackDelayPercent = delayPercent;
            ChampionAttackDelayScaling = delayScaling;
        }

        public static double GetMinInputDelay() => MinInputDelay;
        public static double GetOrderTickRate() => OrderTickRate;
    }
}
