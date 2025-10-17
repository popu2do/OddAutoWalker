<h1 align="center">
    Odd AutoWalker
</h1>
<p align="center">
    <a href="https://github.com/approved/OddAutoWalker/actions?query=workflow%3A%22.NET+Core%22">
        <img src="https://img.shields.io/github/workflow/status/approved/OddAutoWalker/.NET%20Core/master?style=for-the-badge">
    </a>
    <a href="license">
        <img alt="GitHub" src="https://img.shields.io/github/license/approved/OddAutoWalker?style=for-the-badge">
    </a>
    <br>
    <b>
    Get a competitive edge and save your wrists in League of Legends. <br>
    This tool is designed to <a href="https://mobalytics.gg/blog/lol-attack-move-how-to-orb-walk/" title="Orb walking is where you auto attack a target but cancel or finish the animation early by entering a new command that interrupts it.">orb walk</a> optimally by calculating the amount of time it takes to complete an auto attack and automatically issuing both the move and attack commands.
    </b>
</p>

<p align="center">
    <img src="https://odd.dev/videos/league_kogmaw_autowalker.gif">
</p>

---

## How It Works

This project utilizes the [League of Legends Live Client API](https://developer.riotgames.com/docs/lol#game-client-api_live-client-data-api) to get your in-game stats to calculate the appropriate times to issue moves and attacks.
Using [LowLevelInput.Net](https://github.com/michel-pi/LowLevelInput.Net), `OddAutoWalker` is able to capture the user's input and know when to start issuing actions. The actions are hardware emulated using pinvoke and `SendInput` found in the [InputSimulator.cs](OddAutoWalker/InputSimulator.cs) class

---

## 如何获取程序

### 方法一：直接下载（推荐）
1. 点击右上角的 **"Code"** 按钮
2. 选择 **"Download ZIP"** 下载压缩包
3. 解压到任意文件夹
4. 双击 `OddAutoWalker.exe` 运行

### 方法二：编译源码
如果你想要最新版本或自定义修改：

**系统要求：**
- Windows 10/11 系统
- 下载安装 [.NET 9.0 SDK](https://dotnet.microsoft.com/download)

**编译步骤：**
1. 下载源码（方法同上）
2. 解压后打开命令行，进入项目文件夹
3. 运行以下命令：
   ```bash
   dotnet build -c Release
   ```
4. 编译完成后，程序在 `bin\Release\net9.0\win-x64\` 文件夹中

**生成单文件版本（可选）：**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
生成的单文件在 `bin\Release\net9.0\win-x64\publish\OddAutoWalker.exe`

---

## 使用方法

### 重要说明
- 程序需要在游戏内将 **"玩家攻击移动"** 绑定到 **'A'** 键
- 设置位置：游戏内 → 设置 → 热键 → 玩家移动

### 使用步骤

1. **启动程序**
   - 双击 `OddAutoWalker.exe` 运行程序
   - 同时启动英雄联盟游戏

2. **进入游戏**
   - 选择任意模式（除了云顶之弈）
   - 等待进入游戏

3. **激活走A**
   - 按住 **'C'** 键激活自动走A
   - 松开 **'C'** 键停止走A

4. **聊天模式**
   - 按 **回车键** 进入聊天模式（走A暂停）
   - 按 **ESC键** 退出聊天模式

### 功能特点
- ✅ 自动检测游戏进程
- ✅ 智能计算攻击间隔
- ✅ 支持聊天模式暂停
- ✅ 延迟激活防止误触
- ✅ 自适应定时器优化
