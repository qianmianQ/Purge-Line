#nullable disable
namespace Base.BaseSystem.EventSystem.New
{
    [System.Flags]
    internal enum HandlerFlags : byte
    {
        None = 0,
        Removed = 1 << 0,
        Once = 1 << 1,
        WeakReference = 1 << 2,
        CaptureContext = 1 << 3
    }
}
