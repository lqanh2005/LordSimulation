using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Khung hình Idle / Walk (mặt trước + lưng) rút một lần từ AnimatorController rồi dùng chung cho mọi cư dân.
/// Thay cho mỗi người một Animator chạy riêng (2000 người tốn ~10 ms mỗi khung hình).
/// </summary>
public sealed class ResidentSpriteClips
{
    public struct Clip
    {
        public Sprite[] frames;
        public float framesPerSecond;

        public bool IsValid => frames != null && frames.Length > 0;
    }

    private static readonly Dictionary<RuntimeAnimatorController, ResidentSpriteClips> Cache =
        new Dictionary<RuntimeAnimatorController, ResidentSpriteClips>();

    private readonly Clip[] _clips = new Clip[4];

    public ref readonly Clip Get(bool walking, bool back) => ref _clips[(walking ? 1 : 0) | (back ? 2 : 0)];

    /// <summary>null nếu không rút được (khi đó cư dân chạy Animator như cũ).</summary>
    public static ResidentSpriteClips For(RuntimeAnimatorController controller)
    {
        if (controller == null)
            return null;
        if (!Cache.TryGetValue(controller, out ResidentSpriteClips clips))
        {
            clips = Sample(controller);
            Cache[controller] = clips;
        }
        return clips;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Cache.Clear();

    private static ResidentSpriteClips Sample(RuntimeAnimatorController controller)
    {
        GameObject go = new GameObject("ResidentClipSampler") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            Animator animator = go.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = controller;

            var result = new ResidentSpriteClips();
            for (int i = 0; i < 4; i++)
            {
                animator.SetBool(ResidentBase.IsWalkingParam, (i & 1) != 0);
                animator.SetBool(ResidentBase.IsBackParam, (i & 2) != 0);
                for (int s = 0; s < 4; s++)     // để chuyển trạng thái xong
                    animator.Update(0.02f);

                AnimatorClipInfo[] infos = animator.GetCurrentAnimatorClipInfo(0);
                if (infos.Length == 0 || infos[0].clip == null)
                    return null;

                AnimationClip clip = infos[0].clip;
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                int count = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
                Sprite[] frames = new Sprite[count];
                for (int f = 0; f < count; f++)
                {
                    animator.Play(state.fullPathHash, 0, (f + 0.5f) / count);
                    animator.Update(0f);
                    if (sr.sprite == null)
                        return null;
                    frames[f] = sr.sprite;
                }
                result._clips[i] = new Clip { frames = frames, framesPerSecond = clip.frameRate * Mathf.Max(0.01f, state.speed) };
            }
            return result;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
