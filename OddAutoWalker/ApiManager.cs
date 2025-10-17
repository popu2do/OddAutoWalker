using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

namespace OddAutoWalker
{
    public class ApiManager
    {
        private const string ActivePlayerEndpoint = @"https://127.0.0.1:2999/liveclientdata/activeplayer";
        private const string PlayerListEndpoint = @"https://127.0.0.1:2999/liveclientdata/playerlist";
        private const string ChampionStatsEndpoint = @"https://raw.communitydragon.org/latest/game/data/characters/";

        private readonly HttpClient _client;
        private int _apiFailureCount = 0;
        private DateTime _lastApiFailure = DateTime.MinValue;
        private double _estimatedApiLatency = 30; // 默认30ms

        public event Action<string, LogLevel> OnLogMessage;
        public event Action<int> OnApiFailureCountChanged;

        public double EstimatedApiLatency => _estimatedApiLatency;

        public ApiManager()
        {
            _client = CreateHttpClient();
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            return new HttpClient(handler);
        }

        public async Task<JsonDocument> GetActivePlayerData(int maxRetries = 3)
        {
            return await GetApiDataWithRetry(ActivePlayerEndpoint, maxRetries);
        }

        public async Task<JsonDocument> GetPlayerListData(int maxRetries = 3)
        {
            return await GetApiDataWithRetry(PlayerListEndpoint, maxRetries);
        }

        public async Task<JsonDocument> GetChampionStatsData(string championName, int maxRetries = 3)
        {
            string lowerChampionName = championName.ToLower();
            string endpoint = $"{ChampionStatsEndpoint}{lowerChampionName}/{lowerChampionName}.bin.json";
            return await GetApiDataWithRetry(endpoint, maxRetries);
        }

        private async Task<JsonDocument> GetApiDataWithRetry(string endpoint, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var response = await _client.GetStringAsync(endpoint);
                    sw.Stop();
                    
                    // 更新网络延迟估计（移动平均）
                    _estimatedApiLatency = _estimatedApiLatency * 0.8 + sw.ElapsedMilliseconds * 0.2;
                    
                    _apiFailureCount = 0; // 重置失败计数
                    OnApiFailureCountChanged?.Invoke(_apiFailureCount);
                    return JsonDocument.Parse(response);
                }
                catch (HttpRequestException ex)
                {
                    LogMessage($"网络请求失败 (尝试 {i + 1}/{maxRetries}): {endpoint}, 错误: {ex.Message}", LogLevel.Warning);
                    
                    if (i == maxRetries - 1)
                    {
                        HandleApiFailure();
                        return null;
                    }
                    
                    await Task.Delay(100 * (i + 1)); // 递增延迟
                }
                catch (TaskCanceledException)
                {
                    LogMessage($"API请求超时 (尝试 {i + 1}/{maxRetries}): {endpoint}", LogLevel.Warning);
                    
                    if (i == maxRetries - 1)
                    {
                        HandleApiFailure();
                        return null;
                    }
                    
                    await Task.Delay(200 * (i + 1)); // 超时后更长的延迟
                }
                catch (Exception ex)
                {
                    LogMessage($"API调用异常 (尝试 {i + 1}/{maxRetries}): {endpoint}, 错误: {ex.Message}", LogLevel.Error);
                    
                    if (i == maxRetries - 1)
                    {
                        HandleApiFailure();
                        return null;
                    }
                    
                    await Task.Delay(100 * (i + 1));
                }
            }
            return null;
        }

        private void HandleApiFailure()
        {
            _apiFailureCount++;
            _lastApiFailure = DateTime.Now;
            OnApiFailureCountChanged?.Invoke(_apiFailureCount);

            if (_apiFailureCount >= 5)
            {
                LogMessage($"API连续失败 {_apiFailureCount} 次，请检查游戏是否正常运行", LogLevel.Error);
            }
        }

        public void ResetFailureCount()
        {
            _apiFailureCount = 0;
            _lastApiFailure = DateTime.MinValue;
            OnApiFailureCountChanged?.Invoke(_apiFailureCount);
        }

        private void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            OnLogMessage?.Invoke(message, level);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
