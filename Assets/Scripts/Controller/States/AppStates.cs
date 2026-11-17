using UnityEngine;

public enum AppStateID : byte
{
    None = 0,
    Loading = 1,
    Home = 2,
    GamePlay = 3
}

public enum GameStartMode : byte
{
    Auto = 0,
    NewGame = 1,
    Continue = 2
}

public class AppLoadingState : IState<GameController>
{
    // Giữ thanh 100% một chút trước khi chuyển cảnh để người chơi kịp thấy "Hoàn tất"
    private const float HoldAtFullSeconds = 0.35f;

    private float elapsed;
    private float doneTime;

    public void Enter(GameController owner)
    {
        elapsed = 0f;
        doneTime = -1f;
        Time.timeScale = 1f;
        owner.StartPreload();
    }

    public void Tick(GameController owner, float deltaTime)
    {
        elapsed += deltaTime;

        GameDataPreloader preloader = owner.Preloader;
        if (preloader != null && !preloader.IsDone)
            return;
        if (doneTime < 0f)
            doneTime = elapsed;

        if (elapsed >= owner.MinLoadingSeconds && elapsed - doneTime >= HoldAtFullSeconds)
            owner.ChangeState(AppStateID.Home);
    }

    public void Exit(GameController owner)
    {
    }
}

public class AppHomeState : IState<GameController>
{
    public void Enter(GameController owner)
    {
        Time.timeScale = 1f;
        owner.LoadSceneIfNeeded(owner.HomeSceneName);
    }

    public void Tick(GameController owner, float deltaTime)
    {
    }

    public void Exit(GameController owner)
    {
    }
}

public class AppGamePlayState : IState<GameController>
{
    public void Enter(GameController owner)
    {
        Time.timeScale = 1f;
        if (owner.saveLoadManager != null)
            owner.saveLoadManager.ResetAutoSaveTimer();
        owner.LoadSceneIfNeeded(owner.GameplaySceneName);
    }

    public void Tick(GameController owner, float deltaTime)
    {
        GamePlayController gameplay = GamePlayController.Instance;
        if (gameplay == null || !gameplay.IsIn(GamePlayStateID.Playing))
            return;

        if (owner.saveLoadManager != null)
            owner.saveLoadManager.TickAutoSave(deltaTime);
    }

    public void Exit(GameController owner)
    {
        GamePlayController gameplay = GamePlayController.Instance;
        bool canSave = gameplay != null
            && (gameplay.IsIn(GamePlayStateID.Playing) || gameplay.IsIn(GamePlayStateID.Paused)
                || gameplay.IsIn(GamePlayStateID.Victory)); // Về menu từ màn thắng -> lưu cờ đã thắng

        if (canSave && owner.saveLoadManager != null)
            owner.saveLoadManager.SaveGame();

        Time.timeScale = 1f;
    }
}
