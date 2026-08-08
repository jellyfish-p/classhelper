namespace ClassHelper.Core.RollCall;

public sealed record RosterMember(
    Guid Id,
    string Name,
    string? Number = null,
    bool IsActive = true);
