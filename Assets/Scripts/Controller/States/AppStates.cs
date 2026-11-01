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
    private float elapsed;

    public void Enter(GameController owner)
    {
        elapsed = 0f;
        Time.timeScale = 1f;
    }

    public void Tick(GameController owner, float deltaTime)
    {
        elapsed += deltaTime;
        if (elapsed >= owner.MinLoadingSeconds)
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
            && (gameplay.IsIn(GamePlayStateID.Playing) || gameplay.IsIn(GamePlayStateID.Paused));

        if (canSave && owner.saveLoadManager != null)
            owner.saveLoadManager.SaveGame();

        Time.timeScale = 1f;
    }
}
