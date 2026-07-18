namespace FxLink.Statics;

public static class Id
{
    public static Guid New()
    {
#if NET9_0_OR_GREATER
        if (Environment.Version.Major >= 10) return Guid.CreateVersion7();
#endif
        return Guid.NewGuid();
    }
}
