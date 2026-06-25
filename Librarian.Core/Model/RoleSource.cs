namespace Librarian.Model
{
    /// <summary>
    /// Where a file/Item's role assignment came from. The heuristic association pass owns
    /// <see cref="Auto"/> rows and rewrites them on every run; a user correction is <see cref="Manual"/>
    /// and is never clobbered by re-association (plan.md Standing decisions). Manual wins.
    /// </summary>
    public enum RoleSource
    {
        Auto = 0,
        Manual = 1,
    }
}
