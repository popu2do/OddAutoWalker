using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OddAutoWalker
{
    public class ChampionDataManager
    {
        public string ActivePlayerName { get; private set; } = string.Empty;
        public string ChampionName { get; private set; } = string.Empty;
        public string RawChampionName { get; private set; } = string.Empty;

        public double ClientAttackSpeed { get; set; } = 0.625;
        public double ChampionAttackCastTime { get; set; } = 0.625;
        public double ChampionAttackTotalTime { get; set; } = 0.625;
        public double ChampionAttackSpeedRatio { get; set; } = 0.625;
        public double ChampionAttackDelayPercent { get; set; } = 0.3;
        public double ChampionAttackDelayScaling { get; set; } = 1.0;

        public bool IsInitializingValues { get; set; } = false;
        public bool IsUpdatingAttackValues { get; set; } = false;

        private readonly ApiManager _apiManager;

        public event Action<string, LogLevel> OnLogMessage;
        public event Action<string, string> OnChampionInitialized; // (playerName, championName)

        public ChampionDataManager(ApiManager apiManager)
        {
            _apiManager = apiManager;
        }

        public async Task<bool> InitializeChampionData()
        {
            if (!string.IsNullOrEmpty(ChampionName))
                return true;

            IsInitializingValues = true;

            try
            {
                var activePlayerDoc = await _apiManager.GetActivePlayerData();
                if (activePlayerDoc == null)
                {
                    IsInitializingValues = false;
                    return false;
                }

                ActivePlayerName = activePlayerDoc.RootElement.GetProperty("summonerName").GetString();

                var playerListDoc = await _apiManager.GetPlayerListData();
                if (playerListDoc == null)
                {
                    IsInitializingValues = false;
                    return false;
                }

                foreach (var element in playerListDoc.RootElement.EnumerateArray())
                {
                    if (element.GetProperty("summonerName").GetString().Equals(ActivePlayerName))
                    {
                        ChampionName = element.GetProperty("championName").GetString();
                        string[] rawNameArray = element.GetProperty("rawChampionName").GetString().Split('_', StringSplitOptions.RemoveEmptyEntries);
                        RawChampionName = rawNameArray[^1];
                        break;
                    }
                }

                if (!await GetChampionBaseValues(RawChampionName))
                {
                    IsInitializingValues = false;
                    return false;
                }

                LogMessage($"初始化英雄数据: {ChampionName} (玩家: {ActivePlayerName})", LogLevel.Info);
                OnChampionInitialized?.Invoke(ActivePlayerName, ChampionName);

                IsInitializingValues = false;
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"初始化英雄数据失败: {ex.Message}", LogLevel.Error);
                IsInitializingValues = false;
                return false;
            }
        }

        public async Task<bool> UpdateAttackSpeed()
        {
            if (IsUpdatingAttackValues)
                return false;

            IsUpdatingAttackValues = true;

            try
            {
                var activePlayerDoc = await _apiManager.GetActivePlayerData();
                if (activePlayerDoc == null)
                {
                    IsUpdatingAttackValues = false;
                    return false;
                }

                ClientAttackSpeed = activePlayerDoc.RootElement.GetProperty("championStats").GetProperty("attackSpeed").GetDouble();
                IsUpdatingAttackValues = false;
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"更新攻速失败: {ex.Message}", LogLevel.Error);
                IsUpdatingAttackValues = false;
                return false;
            }
        }

        private async Task<bool> GetChampionBaseValues(string championName)
        {
            try
            {
                var championBinDoc = await _apiManager.GetChampionStatsData(championName);
                if (championBinDoc == null)
                {
                    LogMessage($"获取英雄 {championName} 数据失败", LogLevel.Error);
                    return false;
                }

                var championRootStats = championBinDoc.RootElement.GetProperty($"Characters/{championName}/CharacterRecords/Root");
                ChampionAttackSpeedRatio = championRootStats.GetProperty("attackSpeedRatio").GetDouble();

                var championBasicAttackInfo = championRootStats.GetProperty("basicAttack");
                var championAttackDelayOffsetToken = championBasicAttackInfo.TryGetProperty("mAttackDelayCastOffsetPercent", out var attackDelayOffset) ? attackDelayOffset : (JsonElement?)null;
                var championAttackDelayOffsetSpeedRatioToken = championBasicAttackInfo.TryGetProperty("mAttackDelayCastOffsetPercentAttackSpeedRatio", out var attackDelayOffsetSpeedRatio) ? attackDelayOffsetSpeedRatio : (JsonElement?)null;

                if (championAttackDelayOffsetSpeedRatioToken.HasValue)
                {
                    ChampionAttackDelayScaling = championAttackDelayOffsetSpeedRatioToken.Value.GetDouble();
                }

                if (!championAttackDelayOffsetToken.HasValue)
                {
                    var attackTotalTimeToken = championBasicAttackInfo.TryGetProperty("mAttackTotalTime", out var attackTotalTime) ? attackTotalTime : (JsonElement?)null;
                    var attackCastTimeToken = championBasicAttackInfo.TryGetProperty("mAttackCastTime", out var attackCastTime) ? attackCastTime : (JsonElement?)null;

                    if (!attackTotalTimeToken.HasValue && !attackCastTimeToken.HasValue)
                    {
                        string attackName = championBasicAttackInfo.GetProperty("mAttackName").GetString();
                        string attackSpell = $"Characters/{attackName.Split(new[] { "BasicAttack" }, StringSplitOptions.RemoveEmptyEntries)[0]}/Spells/{attackName}";
                        ChampionAttackDelayPercent += championBinDoc.RootElement.GetProperty(attackSpell).GetProperty("mSpell").GetProperty("delayCastOffsetPercent").GetDouble();
                    }
                    else
                    {
                        ChampionAttackTotalTime = attackTotalTimeToken.Value.GetDouble();
                        ChampionAttackCastTime = attackCastTimeToken.Value.GetDouble();
                        ChampionAttackDelayPercent = ChampionAttackCastTime / ChampionAttackTotalTime;
                    }
                }
                else
                {
                    ChampionAttackDelayPercent += championAttackDelayOffsetToken.Value.GetDouble();
                }

                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"获取英雄 {championName} 数据异常: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        public void Reset()
        {
            ActivePlayerName = string.Empty;
            ChampionName = string.Empty;
            RawChampionName = string.Empty;
            ClientAttackSpeed = 0.625;
            ChampionAttackCastTime = 0.625;
            ChampionAttackTotalTime = 0.625;
            ChampionAttackSpeedRatio = 0.625;
            ChampionAttackDelayPercent = 0.3;
            ChampionAttackDelayScaling = 1.0;
            IsInitializingValues = false;
            IsUpdatingAttackValues = false;
        }

        // 计算相关方法
        public double GetSecondsPerAttack() => 1 / ClientAttackSpeed;
        public double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;
        public double GetBufferedWindupDuration() => GetWindupDuration();

        private void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            OnLogMessage?.Invoke(message, level);
        }
    }
}
