using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Nạp asset qua Addressables theo key (address), giữ handle để dùng lại và Release khi không cần.
/// Group / address được tạo bởi menu Tools/LordSimulation/Addressables/Setup Groups.
/// </summary>
public static class GameAssets
{
    // ---------- Address ----------
    public const string UISkinKey = "UI/UISkin";
    public const string GateBackgroundKey = "UI/GateBackground";
    public const string MusicMenuKey = "Music/Menu";
    public const string MusicSpringKey = "Music/Spring";
    public const string MusicSummerKey = "Music/Summer";
    public const string MusicAutumnKey = "Music/Autumn";
    public const string MusicWinterKey = "Music/Winter";

    private static readonly Dictionary<string, AsyncOperationHandle> Handles = new Dictionary<string, AsyncOperationHandle>();

    // Project tắt Domain Reload: handle của lần Play trước đã chết cùng Addressables cũ -> bỏ, không Release
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Handles.Clear();

    /// <summary>Nạp bất đồng bộ; gọi lại cùng key trả về cùng handle (không nạp lại).</summary>
    public static AsyncOperationHandle<T> LoadAsync<T>(string key) where T : Object
    {
        if (Handles.TryGetValue(key, out AsyncOperationHandle existing) && existing.IsValid())
            return existing.Convert<T>();

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        Handles[key] = handle;
        return handle;
    }

    /// <summary>Nạp đồng bộ (chờ xong). Dùng khi cần ngay mà chưa preload; lỗi thì trả null.</summary>
    public static T LoadSync<T>(string key) where T : Object
    {
        AsyncOperationHandle<T> handle = LoadAsync<T>(key);
        T result = handle.WaitForCompletion();
        if (handle.Status == AsyncOperationStatus.Succeeded)
            return result;

        Debug.LogWarning($"[GameAssets] Không nạp được '{key}'. Đã chạy Tools/LordSimulation/Addressables/Setup Groups chưa?");
        Release(key);
        return null;
    }

    /// <summary>Asset đã nạp xong (không chặn); chưa xong / chưa nạp thì null.</summary>
    public static T GetLoaded<T>(string key) where T : Object
    {
        if (Handles.TryGetValue(key, out AsyncOperationHandle handle) && handle.IsValid() && handle.IsDone
            && handle.Status == AsyncOperationStatus.Succeeded)
            return handle.Result as T;
        return null;
    }

    public static void Release(string key)
    {
        if (!Handles.TryGetValue(key, out AsyncOperationHandle handle))
            return;
        Handles.Remove(key);
        if (handle.IsValid())
            Addressables.Release(handle);
    }
}
