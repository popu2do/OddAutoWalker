using System;

namespace OddAutoWalker
{
    /// <summary>
    /// 攻击时间计算器
    /// 负责计算攻击相关的各种时间参数
    /// </summary>
    public static class AttackTimingCalculator
    {
        // ==================== 时间常量（秒） ====================

        /// <summary>
        /// 前摇缓冲时间（秒）
        /// 防止因FPS、网络延迟等因素过早取消攻击
        /// 设置为 1/15 秒 ≈ 66.7ms，确保攻击不会被打断
        /// </summary>
        public const double WINDUP_BUFFER = 1.0 / 15.0;

        /// <summary>
        /// 最小输入延迟（秒）
        /// 防止输入过于频繁导致系统负载过高
        /// 设置为 1/30 秒 ≈ 33.3ms，限制最高30Hz的输入频率
        /// </summary>
        public const double MIN_INPUT_DELAY = 1.0 / 30.0;

        /// <summary>
        /// 定时器检查频率（秒）
        /// 用于走A定时器的间隔时间
        /// 设置为 1/30 秒 ≈ 33.3ms，30Hz的检查频率
        /// </summary>
        public const double TICK_RATE = 1.0 / 30.0;

        /// <summary>
        /// 动态缓冲最小比例
        /// 前摇时间的最小缓冲比例（30%）
        /// </summary>
        public const double MIN_BUFFER_RATIO = 0.3;

        // ==================== 移动频率常量（毫秒） ====================

        /// <summary>
        /// 高攻速时最小移动间隔（毫秒）
        /// 攻速 >= 3.0 时，移动频率为 30Hz
        /// </summary>
        public const double MIN_MOVE_INTERVAL_MS = 33.33;

        /// <summary>
        /// 低攻速时最大移动间隔（毫秒）
        /// 攻速 <= 1.2 时，移动频率为 10Hz
        /// </summary>
        public const double MAX_MOVE_INTERVAL_MS = 100.0;

        /// <summary>
        /// 默认低攻速阈值
        /// 攻速低于此值时使用最大移动间隔
        /// </summary>
        public const double DEFAULT_LOW_AS_THRESHOLD = 1.2;

        /// <summary>
        /// 默认高攻速阈值
        /// 攻速高于此值时使用最小移动间隔
        /// </summary>
        public const double DEFAULT_HIGH_AS_THRESHOLD = 3.0;

        // 攻击速度相关（从游戏API获取）
        private static double ClientAttackSpeed = 0.625;           // 客户端攻击速度（次/秒）
        private static double ChampionAttackCastTime = 0.625;      // 英雄攻击施法时间（前摇）
        private static double ChampionAttackTotalTime = 0.625;     // 英雄攻击总时间
        private static double ChampionAttackSpeedRatio = 0.625;    // 英雄攻击速度比例
        private static double ChampionAttackDelayPercent = 0.3;    // 攻击延迟百分比
        private static double ChampionAttackDelayScaling = 1.0;    // 攻击延迟缩放系数

        public static double GetSecondsPerAttack() => 1 / ClientAttackSpeed;
        public static double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;
        public static double GetBufferedWindupDuration() => GetWindupDuration() + WINDUP_BUFFER;

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

        public static double GetMinInputDelay() => MIN_INPUT_DELAY;
        public static double GetOrderTickRate() => TICK_RATE;
        public static double GetTickRateMs() => TICK_RATE * 1000.0;
    }
}
