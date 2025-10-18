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

        /// <summary>
        /// 获取每秒攻击次数
        /// </summary>
        public static double GetSecondsPerAttack() => 1 / ClientAttackSpeed;

        /// <summary>
        /// 获取攻击前摇持续时间
        /// </summary>
        public static double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;

        /// <summary>
        /// 获取带缓冲的攻击前摇持续时间
        /// </summary>
        public static double GetBufferedWindupDuration() => GetWindupDuration() + WindupBuffer;

        /// <summary>
        /// 计算计算刻间隔（Tick Interval）
        /// 
        /// 计算刻是系统检查"是否可以攻击/移动"的频率，类似于游戏中的Tick系统
        /// 计算刻频率越高，走A越精确，但CPU占用也越高
        /// 
        /// 调整方法：
        /// 1. 基础频率：每次攻击周期内进行10次检查，确保精确度
        /// 2. 上限限制：受 MaxCheckFrequencyHz 限制，防止CPU占用过高
        /// 3. 下限保护：根据攻速动态调整，攻速越高检查间隔越短
        /// 
        /// 示例：
        /// - 攻速1.0：攻击间隔1秒，计算刻间隔100ms（10Hz）
        /// - 攻速2.0：攻击间隔0.5秒，计算刻间隔50ms（20Hz）
        /// - 攻速4.0：攻击间隔0.25秒，计算刻间隔25ms（40Hz）
        /// 
        /// </summary>
        /// <returns>计算刻间隔（毫秒）</returns>
        public static double GetTickInterval(Settings settings)
        {
            var secondsPerAttack = GetSecondsPerAttack();
            
            // 目标：每次攻击周期内进行8-12次检查，确保精确度
            var idealChecksPerAttack = 10; // 每次攻击检查10次
            var idealIntervalMs = (secondsPerAttack / idealChecksPerAttack) * 1000;
            
            // 上限：受 MaxCheckFrequencyHz 限制，防止CPU占用过高
            // 例如：120Hz = 1000ms / 120 = 8.33ms
            var maxFrequencyHz = settings.MaxCheckFrequencyHz;
            var minIntervalMs = 1000.0 / maxFrequencyHz;
            
            // 下限：根据攻速动态调整，攻速越高，检查间隔越短
            // 但不能超过最大频率限制
            var tickIntervalMs = Math.Max(idealIntervalMs, minIntervalMs);
            
            return tickIntervalMs;
        }

        /// <summary>
        /// 计算走A移动间隔
        /// 
        /// 移动间隔是两次移动指令之间的时间间隔，影响走A的流畅度
        /// 间隔太短：移动太频繁可能导致游戏掉线或无效微操
        /// 间隔太长：影响走位效果，无法及时躲避技能
        /// 
        /// 计算方法：
        /// 1. 基础比例：移动间隔 = 攻击间隔 × 30-50%
        /// 2. 对数曲线：使用对数函数让移动间隔随攻击间隔增长，但增长幅度递减
        /// 3. 范围限制：确保在 MinMoveCommandIntervalSeconds 和 MaxMoveCommandIntervalSeconds 之间
        /// 
        /// 不同攻速的处理：
        /// - 低攻速英雄：攻击间隔长，移动间隔也长，注重走位躲技能
        /// - 高攻速英雄：攻击间隔短，移动间隔也短，获得丝滑走A体验
        /// - 攻速极高时：移动间隔接近最小值，避免无意义的频繁移动
        /// 
        /// 示例：
        /// - 攻速1.0（1秒攻击）：移动间隔约300-500ms
        /// - 攻速2.0（0.5秒攻击）：移动间隔约150-250ms  
        /// - 攻速4.0（0.25秒攻击）：移动间隔约75-125ms
        /// 
        /// </summary>
        /// <returns>移动间隔（秒）</returns>
        public static double GetOrbWalkMoveInterval(Settings settings)
        {
            var secondsPerAttack = GetSecondsPerAttack();
            
            // 移动间隔与攻击间隔的关系
            // 目标：攻击间隔越长，移动间隔也越长，但增长幅度递减
            
            // 1. 基础移动间隔：攻击间隔的30-50%
            // 使用对数曲线，让移动间隔随攻击间隔增长但增长幅度递减
            var baseRatio = 0.3 + (0.2 * Math.Log(1 + secondsPerAttack) / Math.Log(2)); // 0.3-0.5范围
            
            // 2. 计算移动间隔
            var moveInterval = secondsPerAttack * baseRatio;
            
            // 3. 限制范围：确保在 min/max 之间
            var minInterval = settings.MinMoveCommandIntervalSeconds;  // 防止过于频繁导致掉线
            var maxInterval = settings.MaxMoveCommandIntervalSeconds;  // 防止间隔过长影响走位
            
            return Math.Max(Math.Min(moveInterval, maxInterval), minInterval);
        }

        /// <summary>
        /// 更新攻击速度数据
        /// </summary>
        public static void UpdateAttackSpeed(double attackSpeed)
        {
            ClientAttackSpeed = attackSpeed;
        }

        /// <summary>
        /// 更新英雄攻击数据
        /// </summary>
        public static void UpdateChampionAttackData(double castTime, double totalTime, double speedRatio, double delayPercent, double delayScaling)
        {
            ChampionAttackCastTime = castTime;
            ChampionAttackTotalTime = totalTime;
            ChampionAttackSpeedRatio = speedRatio;
            ChampionAttackDelayPercent = delayPercent;
            ChampionAttackDelayScaling = delayScaling;
        }

        /// <summary>
        /// 获取最小输入延迟
        /// </summary>
        public static double GetMinInputDelay() => MinInputDelay;

        /// <summary>
        /// 获取指令检查频率
        /// </summary>
        public static double GetOrderTickRate() => OrderTickRate;
    }
}
