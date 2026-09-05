using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance;
    public MusicManager musicManager;
    public SaveLoadManager saveLoadManager;
    [HideInInspector] public SceneType currentScene;
    private void Awake()
    {
        Instance = this;
        Init();
        DontDestroyOnLoad(this);
    }

    private void Init()
    {
        Application.targetFrameRate = 60;
        Setup();

    }

    private void Setup()
    {
        saveLoadManager.Init();
    }
    public void LoadScene(string sceneName)
    {
        Initiate.Fade(sceneName.ToString(), Color.black, 2f);
    }
}
public enum SceneType
{
    StartLoading = 0,
    MainHome = 1,
    GamePlay = 2
}