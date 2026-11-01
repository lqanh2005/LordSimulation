public enum GamePlayStateID : byte
{
    None = 0,
    Init = 1,
    Playing = 2,
    Paused = 3,
    GameOver = 4
}

public class GamePlayInitState : IState<GamePlayController>
{
    public void Enter(GamePlayController owner)
    {
        if (owner.InitializeSession())
            owner.ChangeState(GamePlayStateID.Playing);
    }

    public void Tick(GamePlayController owner, float deltaTime)
    {
    }

    public void Exit(GamePlayController owner)
    {
    }
}

public class GamePlayPlayingState : IState<GamePlayController>
{
    public void Enter(GamePlayController owner)
    {
    }

    public void Tick(GamePlayController owner, float deltaTime)
    {
    }

    public void Exit(GamePlayController owner)
    {
    }
}

public class GamePlayPausedState : IState<GamePlayController>
{
    private GameSpeed resumeSpeed = GameSpeed.Speed1x;

    public GameSpeed SpeedBeforePause { get; private set; } = GameSpeed.Speed1x;

    public void Enter(GamePlayController owner)
    {
        CaptureAndPause(owner);
    }

    public void CaptureAndPause(GamePlayController owner)
    {
        GlobalSystemManager global = owner.GlobalSystem;
        if (global == null)
            return;

        GameSpeed current = global.GetGlobalDataRef().currentGameSpeed;
        SpeedBeforePause = current;
        resumeSpeed = current == GameSpeed.Pause ? GameSpeed.Speed1x : current;
        global.SetGameSpeed(GameSpeed.Pause);
    }

    public void Tick(GamePlayController owner, float deltaTime)
    {
    }

    public void Exit(GamePlayController owner)
    {
        GlobalSystemManager global = owner.GlobalSystem;
        if (global != null)
            global.SetGameSpeed(resumeSpeed);
    }
}

public class GamePlayOverState : IState<GamePlayController>
{
    public void Enter(GamePlayController owner)
    {
        GlobalSystemManager global = owner.GlobalSystem;
        if (global != null)
            global.SetGameSpeed(GameSpeed.Pause);

        GameEvents.Post(EventID.GameOver);
    }

    public void Tick(GamePlayController owner, float deltaTime)
    {
    }

    public void Exit(GamePlayController owner)
    {
    }
}
