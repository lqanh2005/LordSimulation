using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance;
    public bool m_DontDestroyOnLoad = true;

    public static bool HasInstance => Instance != null;

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;
            if (transform.parent == null && m_DontDestroyOnLoad)
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }
        else
        {
            if (this != Instance)
            {
                DestroyImmediate(this.gameObject);
            }
            return;
        }
        OnAwake();
    }

    protected void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        OnDestroyed();
    }

    protected virtual void OnAwake()
    {
    }

    protected virtual void OnDestroyed()
    {
    }
}
