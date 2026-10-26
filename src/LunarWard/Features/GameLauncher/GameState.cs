namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 启动器主按钮的形态。<b>原样搬自</b> <c>Starward/src/Starward/Features/GameLauncher/GameState.cs</c>。
/// </summary>
public enum GameState
{

    None = 0,


    StartGame = 1,


    GameIsRunning = 2,


    InstallGame = 3,


    UpdateGame = 4,


    UpdatePlugin = 5,


    Installing = 6,


    ResumeDownload = 7,


    ComingSoon = 8,

}
