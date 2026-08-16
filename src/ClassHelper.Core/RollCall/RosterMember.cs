namespace ClassHelper.Core.RollCall;

public sealed record RosterMember(
    Guid Id,
    string Name,
    string? Number = null,
    bool IsActive = true)
{
    public string DisplayLabel => !string.IsNullOrWhiteSpace(Name)
        ? Name
        : !string.IsNullOrWhiteSpace(Number)
            ? Number
            : "未命名";
}
