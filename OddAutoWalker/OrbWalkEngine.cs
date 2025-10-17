using System;
using System.Diagnostics;
using System.Timers;

namespace OddAutoWalker
{
    public class OrbWalkEngine
    {
        private readonly GameStateManager _gameStateManager;
        private readonly ChampionDataManager _championDataManager;
        private readonly Settings _settings;

        private System.Timers.Timer _orbWalkTimer;
        private bool _orbWalkerTimerActive = false;

        // When these DateTime instances are in the past, the action they gate can be taken
        private DateTime _nextInput = default;
        private DateTime _nextMove = default;
        private DateTime _nextAttack = default;

        private readonly Stopwatch _owStopWatch = new Stopwatch();


        public event Action<string, LogLevel> OnLogMessage;

        public bool IsActive => _orbWalkerTimerActive;

        public OrbWalkEngine(GameStateManager gameStateManager, ChampionDataManager championDataManager, Settings settings)
        {
            _gameStateManager = gameStateManager;
            _championDataManager = championDataManager;
            _settings = settings;
        }

        public void Initialize(double timerInterval)
        {
            _orbWalkTimer = new System.Timers.Timer(timerInterval);
            _orbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
        }

        public void Start()
        {
            if (!_orbWalkerTimerActive)
            {
                _orbWalkerTimerActive = true;
                _orbWalkTimer?.Start();
                LogMessage("走A功能已激活", LogLevel.Info);
            }
        }

        public void Stop()
        {
            if (_orbWalkerTimerActive)
            {
                _orbWalkerTimerActive = false;
                _orbWalkTimer?.Stop();
                LogMessage("走A功能已停用", LogLevel.Info);
            }
        }

        public void UpdateTimerInterval(double newInterval)
        {
            if (_orbWalkTimer != null)
            {
                _orbWalkTimer.Interval = newInterval;
            }
        }

        public void Dispose()
        {
            _orbWalkTimer?.Stop();
            _orbWalkTimer?.Dispose();
        }

        private void OrbWalkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_gameStateManager.IsGameActive())
            {
                return;
            }

            // 检查聊天模式超时和C键延迟激活
            CheckChatModeAndActivation();

            // Store time at timer tick start into a variable for readability
            var time = e.SignalTime;

            // Make sure we can send input without being dropped
            // This is used for gating movement orders when waiting for an attack to be prepared
            // This is not needed if this function is not ran frequently enough for it to matter
            // If it isn't, you might end up with this timer and this function's timer being out of sync
            //   resulting in a (worst-case) OrderTickRate + MinInputDelay delay
            // It is currently disabled due to this, enable it if you want/need to
            if (true || _nextInput < time)
            {
                // If we can attack, do so
                if (_nextAttack < time)
                {
                    // Store current time + input delay so we're aware when we can move next
                    _nextInput = time.AddSeconds(GetEffectiveMinInputDelay());

                    // Send attack input
                    InputSimulator.Keyboard.KeyDown((ushort)DirectInputKeys.DIK_A);
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Left);
                    InputSimulator.Keyboard.KeyUp((ushort)DirectInputKeys.DIK_A);

                    // We've sent input now, so we're re-fetching time as I have no idea how long input takes
                    // I'm assuming it's negligable, but why not
                    // Please check what the actual difference is if you consider keeping this lol
                    var attackTime = DateTime.Now;

                    // Store timings for when to next attack / move
                    _nextMove = attackTime.AddSeconds(_championDataManager.GetBufferedWindupDuration());
                    _nextAttack = attackTime.AddSeconds(_championDataManager.GetSecondsPerAttack());
                }
                // If we can't attack but we can move, do so
                else if (_nextMove < time)
                {
                    // Store current time + input delay so we're aware when we can attack / move next
                    _nextInput = time.AddSeconds(GetEffectiveMinInputDelay());

                    // Send move input
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Right);
                }
            }
        }

        private void CheckChatModeAndActivation()
        {
            var now = DateTime.Now;
            
            // 检查聊天模式超时（30秒）
            if (_gameStateManager.IsInChatMode && (now - _gameStateManager.LastChatActivity).TotalSeconds > 30)
            {
                _gameStateManager.IsInChatMode = false;
                LogMessage("聊天模式超时，自动退出", LogLevel.Info);
            }
            
            // 检查走A激活键延迟激活
            if (_gameStateManager.OrbWalkKeyPressed && !_orbWalkerTimerActive && !_gameStateManager.IsInChatMode)
            {
                var holdTime = (now - _gameStateManager.OrbWalkKeyPressStart).TotalMilliseconds;
                if (holdTime >= 150) // 150ms延迟
                {
                    Start();
                }
            }
        }

        private double GetEffectiveMinInputDelay()
        {
            return _settings.MinInputDelayMs < 0 
                ? GetAdaptiveInputDelay() / 1000.0
                : _settings.MinInputDelayMs / 1000.0;
        }

        private double GetAdaptiveInputDelay()
        {
            // 基于攻速的最小输入间隔
            var secondsPerAttack = _championDataManager.GetSecondsPerAttack();
            return Math.Max(secondsPerAttack / 20.0 * 1000, 10); // 最少10ms
        }

        private void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            OnLogMessage?.Invoke(message, level);
        }
    }
}
