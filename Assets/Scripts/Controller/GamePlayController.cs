using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    None,
    Start,
    Playing,
    Pause,
    GameOver
}
public class GamePlayController : Singleton<GamePlayController>
{
    public GameState State;
    public PlayerContain playerContain;
    public GameScene scene;
    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        playerContain.Init();
        if (GameController.Instance.saveLoadManager.HasSaveFile())
            GameController.Instance.saveLoadManager.LoadGame();
        else
            playerContain.globalSystemManager.InitializeNewGame();
    }
}