using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OddAutoWalker
{
    public class GameStateManager
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public bool HasProcess { get; private set; } = false;
        public bool IsExiting { get; set; } = false;
        public Process LeagueProcess { get; private set; } = null;

        // 聊天模式相关变量
        public bool IsInChatMode { get; set; } = false;
        public DateTime LastChatActivity { get; set; } = DateTime.MinValue;

        // 走A激活键相关变量
        public DateTime OrbWalkKeyPressStart { get; set; } = DateTime.MinValue;
        public bool OrbWalkKeyPressed { get; set; } = false;

        public event Action OnProcessDetected;
        public event Action OnProcessLost;

        public bool IsGameActive()
        {
            return HasProcess && !IsExiting && GetForegroundWindow() == LeagueProcess?.MainWindowHandle;
        }

        public void SetProcess(Process process)
        {
            LeagueProcess = process;
            HasProcess = true;
            LeagueProcess.EnableRaisingEvents = true;
            LeagueProcess.Exited += LeagueProcess_Exited;
            OnProcessDetected?.Invoke();
        }

        public void ClearProcess()
        {
            HasProcess = false;
            LeagueProcess = null;
            OnProcessLost?.Invoke();
        }

        private void LeagueProcess_Exited(object sender, EventArgs e)
        {
            ClearProcess();
        }

        public void ResetChatMode()
        {
            IsInChatMode = false;
            LastChatActivity = DateTime.MinValue;
        }

        public void ResetOrbWalkKey()
        {
            OrbWalkKeyPressed = false;
        }

        public void ResetAll()
        {
            ResetChatMode();
            ResetOrbWalkKey();
        }
    }
}
