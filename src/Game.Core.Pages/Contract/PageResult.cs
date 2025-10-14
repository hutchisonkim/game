namespace Game.Core.Pages.Contract;

public record PageResult(
    bool Success,
    string? Message,
    object? Data
);
