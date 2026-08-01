namespace Atlas.Kernel;

// Domain errors carry codes, not just messages — codes are stable and testable; messages are not
// (docs/05-engineering/02-coding-standards.md §2).
public sealed record DomainError(string Code, string Message)
{
    public static DomainError Of(string code, string message) => new(code, message);
}
