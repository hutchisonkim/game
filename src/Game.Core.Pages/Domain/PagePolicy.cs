namespace Game.Core.Pages.Domain;

public static class PagePolicy
{
    public static bool CanExecute(string actionType, string userRole)
    {
        if (actionType == "ViewOnly" && userRole == "Guest") return true;
        if (userRole == "Player") return true;
        return false;
    }
}
