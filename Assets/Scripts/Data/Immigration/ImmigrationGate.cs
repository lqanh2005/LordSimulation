using UnityEngine;

public class ImmigrationGate : MonoBehaviour
{
    [SerializeField] private ImmigrationManager immigrationManager;
    [SerializeField] private TextMesh waitingBadge;
    [SerializeField] private Vector3 badgeOffset = new Vector3(0f, 1.3f, 0f);

    public void Bind(ImmigrationManager manager)
    {
        immigrationManager = manager;
        EnsureBadge();
        Subscribe();
        SetWaitingCount(manager != null ? manager.waitingCount : 0);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
        GameEvents.Listen(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Listen(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void Unsubscribe()
    {
        GameEvents.Unlisten(EventID.ImmigrationQueueChanged, OnQueueChanged);
        GameEvents.Unlisten(EventID.LoadCompleted, OnLoadCompleted);
    }

    private void OnQueueChanged(object param)
    {
        int count = param is int waiting ? waiting : (immigrationManager != null ? immigrationManager.waitingCount : 0);
        SetWaitingCount(count);
    }

    private void OnLoadCompleted(object param)
    {
        if (immigrationManager != null)
            SetWaitingCount(immigrationManager.waitingCount);
    }

    private void OnMouseDown()
    {
        if (immigrationManager == null)
            immigrationManager = FindObjectOfType<ImmigrationManager>();
        if (immigrationManager != null)
            immigrationManager.OpenInspection();
    }

    public void SetWaitingCount(int count)
    {
        EnsureBadge();
        if (waitingBadge == null)
            return;

        waitingBadge.text = count > 0 ? count.ToString() : "";
        waitingBadge.gameObject.SetActive(count > 0);
    }

    private void EnsureBadge()
    {
        if (waitingBadge != null)
            return;

        Transform existing = transform.Find("WaitingBadge");
        GameObject badgeGo = existing != null ? existing.gameObject : new GameObject("WaitingBadge");
        badgeGo.transform.SetParent(transform, false);
        badgeGo.transform.localPosition = badgeOffset;
        waitingBadge = badgeGo.GetComponent<TextMesh>();
        if (waitingBadge == null)
            waitingBadge = badgeGo.AddComponent<TextMesh>();
        waitingBadge.anchor = TextAnchor.MiddleCenter;
        waitingBadge.alignment = TextAlignment.Center;
        waitingBadge.fontSize = 32;
        waitingBadge.characterSize = 0.08f;
        waitingBadge.color = Color.yellow;
        MeshRenderer mesh = badgeGo.GetComponent<MeshRenderer>();
        if (mesh != null)
            mesh.sortingOrder = 25;
    }
}
