public interface IState<in TOwner>
{
    void Enter(TOwner owner);
    void Tick(TOwner owner, float deltaTime);
    void Exit(TOwner owner);
}
