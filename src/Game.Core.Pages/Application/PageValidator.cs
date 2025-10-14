namespace Game.Core.Pages.Application;

public static class PageValidator
{
    public static bool ValidateCommand(string actionType)
        => !string.IsNullOrWhiteSpace(actionType);
}
