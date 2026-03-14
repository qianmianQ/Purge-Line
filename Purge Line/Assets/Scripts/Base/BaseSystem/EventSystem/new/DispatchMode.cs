#nullable disable
namespace Base.BaseSystem.EventSystem.New
{
    public enum DispatchMode
    {
        Immediate,
        Queue,
        MainThread,
        ThreadPool,
        CustomScheduler
    }
}
