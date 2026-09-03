using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance;
    public bool m_DontDestroyOnLoad = true;
    private void Awake()
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
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    protected virtual void OnAwake()
    {
    }
}
