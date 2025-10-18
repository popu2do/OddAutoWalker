using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace OddAutoWalker
{
    /// <summary>
    /// 游戏状态管理器
    /// 负责管理LOL进程状态、英雄信息和API调用
    /// </summary>
    public static class GameStateManager
    {
        private const string ActivePlayerEndpoint = @"https://127.0.0.1:2999/liveclientdata/activeplayer";
        private const string PlayerListEndpoint = @"https://127.0.0.1:2999/liveclientdata/playerlist";
        private const string ChampionStatsEndpoint = @"https://raw.communitydragon.org/latest/game/data/characters/";

        // 游戏状态
        private static bool HasProcess = false;                    // 是否检测到LOL进程
        private static bool IsExiting = false;                     // 是否正在退出
        private static bool IsIntializingValues = false;            // 是否正在初始化英雄数据
        private static bool IsUpdatingAttackValues = false;        // 是否正在更新攻击数据

        // 主要组件
        private static readonly WebClient Client = new WebClient();         // HTTP客户端
        private static Process LeagueProcess = null;                        // LOL进程引用

        // 英雄信息
        private static string ActivePlayerName = string.Empty;      // 当前玩家名称
        private static string ChampionName = string.Empty;         // 英雄显示名称
        private static string RawChampionName = string.Empty;      // 英雄原始名称（用于API）

        // API状态监控
        private static int apiCallCount = 0;                       // API调用次数
        private static DateTime lastApiCall = DateTime.Now;        // 上次API调用时间
        private static double apiLatency = 0;                      // API延迟（毫秒）

        public static void Initialize()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            Client.Proxy = null;
        }

        public static void CheckLeagueProcess()
        {
            while (LeagueProcess is null || !HasProcess)
            {
                LeagueProcess = Process.GetProcessesByName("League of Legends").FirstOrDefault();
                if (LeagueProcess is null || LeagueProcess.HasExited)
                {
                    continue;
                }
                HasProcess = true;
                LeagueProcess.EnableRaisingEvents = true;
                LeagueProcess.Exited += LeagueProcess_Exited;
            }
        }

        public static Process GetLeagueProcess() => LeagueProcess;
        public static bool HasLeagueProcess() => HasProcess;
        public static bool IsExitingProcess() => IsExiting;
        public static bool IsInitializing() => IsIntializingValues;
        public static bool IsUpdatingAttackData() => IsUpdatingAttackValues;
        public static string GetActivePlayerName() => ActivePlayerName;
        public static string GetChampionName() => ChampionName;
        public static string GetRawChampionName() => RawChampionName;
        public static int GetApiCallCount() => apiCallCount;
        public static double GetApiLatency() => apiLatency;
        
        public static void UpdateAttackSpeedData()
        {
            if (HasProcess && !IsExiting && !IsIntializingValues && !IsUpdatingAttackValues)
            {
                IsUpdatingAttackValues = true;

                JToken activePlayerToken = null;
                try
                {
                    var apiStartTime = DateTime.Now;
                    activePlayerToken = JToken.Parse(Client.DownloadString(ActivePlayerEndpoint));
                    apiLatency = (DateTime.Now - apiStartTime).TotalMilliseconds;
                    apiCallCount++;
                    lastApiCall = DateTime.Now;
                }
                catch
                {
                    IsUpdatingAttackValues = false;
                    return;
                }

                if (string.IsNullOrEmpty(ChampionName))
                {
                    ActivePlayerName = activePlayerToken?["summonerName"].ToString();
                    IsIntializingValues = true;
                    JToken playerListToken = JToken.Parse(Client.DownloadString(PlayerListEndpoint));
                    foreach (JToken token in playerListToken)
                    {
                        if (token["summonerName"].ToString().Equals(ActivePlayerName))
                        {
                            ChampionName = token["championName"].ToString();
                            string[] rawNameArray = token["rawChampionName"].ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
                            RawChampionName = rawNameArray[^1];
                        }
                    }

                    if (!GetChampionBaseValues(RawChampionName))
                    {
                        IsIntializingValues = false;
                        IsUpdatingAttackValues = false;
                        return;
                    }

#if DEBUG
                    Console.Title = $"({ActivePlayerName}) {ChampionName}";
#endif

                    IsIntializingValues = false;
                }

                var attackSpeed = activePlayerToken["championStats"]["attackSpeed"].Value<double>();
                AttackTimingCalculator.UpdateAttackSpeed(attackSpeed);
                
                IsUpdatingAttackValues = false;
            }
        }

        private static bool GetChampionBaseValues(string championName)
        {
            string lowerChampionName = championName.ToLower();
            JToken championBinToken = null;
            try
            {
                championBinToken = JToken.Parse(Client.DownloadString($"{ChampionStatsEndpoint}{lowerChampionName}/{lowerChampionName}.bin.json"));
            }
            catch
            {
                return false;
            }
            JToken championRootStats = championBinToken[$"Characters/{championName}/CharacterRecords/Root"];
            var attackSpeedRatio = championRootStats["attackSpeedRatio"].Value<double>();

            JToken championBasicAttackInfoToken = championRootStats["basicAttack"];
            JToken championAttackDelayOffsetToken = championBasicAttackInfoToken["mAttackDelayCastOffsetPercent"];
            JToken championAttackDelayOffsetSpeedRatioToken = championBasicAttackInfoToken["mAttackDelayCastOffsetPercentAttackSpeedRatio"];

            var attackDelayScaling = 1.0;
            if (championAttackDelayOffsetSpeedRatioToken?.Value<double?>() != null)
            {
                attackDelayScaling = championAttackDelayOffsetSpeedRatioToken.Value<double>();
            }

            // 始终尝试获取攻击时间数据
            JToken attackTotalTimeToken = championBasicAttackInfoToken["mAttackTotalTime"];
            JToken attackCastTimeToken = championBasicAttackInfoToken["mAttackCastTime"];

            var attackDelayPercent = 0.3;
            var attackCastTime = 0.625;
            var attackTotalTime = 0.625;

            if (attackTotalTimeToken?.Value<double?>() != null && attackCastTimeToken?.Value<double?>() != null)
            {
                // 有完整的攻击时间数据
                attackTotalTime = attackTotalTimeToken.Value<double>();
                attackCastTime = attackCastTimeToken.Value<double>();
                attackDelayPercent = attackCastTime / attackTotalTime;
            }
            else if (championAttackDelayOffsetToken?.Value<double?>() != null)
            {
                // 使用延迟偏移百分比
                attackDelayPercent += championAttackDelayOffsetToken.Value<double>();
            }
            else
            {
                // 尝试从攻击技能获取
                string attackName = championBasicAttackInfoToken["mAttackName"].ToString();
                string attackSpell = $"Characters/{attackName.Split(new[] { "BasicAttack" }, StringSplitOptions.RemoveEmptyEntries)[0]}/Spells/{attackName}";
                attackDelayPercent += championBinToken[attackSpell]["mSpell"]["delayCastOffsetPercent"].Value<double>();
            }

            AttackTimingCalculator.UpdateChampionAttackData(attackCastTime, attackTotalTime, attackSpeedRatio, attackDelayPercent, attackDelayScaling);
            return true;
        }

        private static void LeagueProcess_Exited(object sender, EventArgs e)
        {
            HasProcess = false;
            LeagueProcess = null;
            Console.WriteLine("League Process Exited");
            CheckLeagueProcess();
        }
    }
}
