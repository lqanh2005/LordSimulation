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
        SaveLoadManager saveLoad = GameController.Instance.saveLoadManager;

        if (saveLoad.HasSaveFile())
            saveLoad.LoadGame();
        else
            playerContain.globalSystemManager.Init();
    }
}