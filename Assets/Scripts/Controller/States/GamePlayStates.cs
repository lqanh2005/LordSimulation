using UnityEngine;

public enum GamePlayStateID : byte
{
    None = 0,
    Init = 1,
    Playing = 2,
    Paused = 3,
    GameOver = 4,
    Victory = 5
}

/// <summary>
/// Đạt mục tiêu: dừng game, hiện popup chiến thắng. "Tiếp tục chơi" quay lại Playing với tốc độ cũ.
/// </summary>
public class GamePlayVictoryState : IState<GamePlayController>
{
    private GameSpeed resumeSpeed = GameSpeed.Speed1x;

    public void Enter(GamePlayController owner)
    {
        GlobalSystemManager global = owner.GlobalSystem;
        var payload = new VictoryPayload();
        if (global != null)
        {
            ref GlobalSystemData data = ref global.GetGlobalDataRef();
            resumeSpeed = data.currentGameSpeed == GameSpeed.Pause ? GameSpeed.Speed1x : data.currentGameSpeed;
            payload.year = data.currentYear;
            payload.month = data.currentMonth;
            payload.population = data.totalPopulation;
            payload.wintersSurvived = Mathf.Max(0, data.currentYear - 1);
            global.SetGameSpeed(GameSpeed.Pause);
        }

        GameEvents.Post(EventID.Victory, payload);
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
        var payload = new GameOverPayload { reason = owner.LastGameOverReason };
        if (global != null)
        {
            global.SetGameSpeed(GameSpeed.Pause);
            ref GlobalSystemData data = ref global.GetGlobalDataRef();
            payload.year = data.currentYear;
            payload.month = data.currentMonth;
        }

        GameEvents.Post(EventID.GameOver, payload);
    }

    public void Tick(GamePlayController owner, float deltaTime)
    {
    }

    public void Exit(GamePlayController owner)
    {
    }
}
