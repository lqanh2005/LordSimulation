using System;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine<TOwner, TStateId> where TStateId : struct, Enum
{
    private readonly TOwner owner;
    private readonly Dictionary<TStateId, IState<TOwner>> states = new Dictionary<TStateId, IState<TOwner>>();
    private IState<TOwner> current;
    private bool isTransitioning;
    private bool hasPending;
    private TStateId pendingId;

    public TStateId CurrentId { get; private set; }
    public TStateId PreviousId { get; private set; }

    public event Action<TStateId, TStateId> StateChanged;

    public StateMachine(TOwner owner)
    {
        this.owner = owner;
    }

    public void Register(TStateId id, IState<TOwner> state)
    {
        states[id] = state;
    }

    public T GetState<T>(TStateId id) where T : class, IState<TOwner>
    {
        return states.TryGetValue(id, out IState<TOwner> state) ? state as T : null;
    }

    public bool IsIn(TStateId id) =>
        current != null && EqualityComparer<TStateId>.Default.Equals(CurrentId, id);

    public bool ChangeState(TStateId id)
    {
        if (!states.TryGetValue(id, out IState<TOwner> next))
        {
            Debug.LogError($"[StateMachine] Chưa đăng ký state {id}.");
            return false;
        }

        if (isTransitioning)
        {
            pendingId = id;
            hasPending = true;
            return true;
        }

        if (IsIn(id))
            return false;

        isTransitioning = true;
        TStateId from = CurrentId;
        current?.Exit(owner);

        PreviousId = from;
        CurrentId = id;
        current = next;
        current.Enter(owner);
        isTransitioning = false;

        StateChanged?.Invoke(from, id);

        if (hasPending)
        {
            hasPending = false;
            ChangeState(pendingId);
        }

        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!isTransitioning)
            current?.Tick(owner, deltaTime);
    }
}
