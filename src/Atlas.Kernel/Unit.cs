namespace Atlas.Kernel;

// The "no value" type for a Result<T> that only signals success or failure — a validation step,
// not a construction. Avoids reaching for exceptions where Result<T> is called for but there's
// nothing to return on success.
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
