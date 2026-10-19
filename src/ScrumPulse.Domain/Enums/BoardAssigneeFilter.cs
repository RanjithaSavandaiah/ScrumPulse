namespace ScrumPulse.Domain.Enums;

/// <summary>
/// Which work items a board sync should pull, by who they are assigned to.
/// </summary>
/// <remarks>
/// A shared Azure DevOps project usually holds work for several groups at once - an offshore
/// delivery squad, a client's own developers, and whoever else touches the product. Pulling
/// all of it would bury the squad's own delivery signal in other people's work and make every
/// cycle-time figure meaningless.
/// </remarks>
public enum BoardAssigneeFilter
{
    /// <summary>
    /// Only items assigned to someone on this squad's roster.
    /// </summary>
    /// <remarks>
    /// The default, and the one that stays correct without maintenance: the roster is already
    /// kept current for capacity and standups, so it doubles as the filter. It is also the
    /// most defensible position on personal data - the sync only ever retrieves records about
    /// people deliberately added to the squad, rather than everyone in the project.
    /// </remarks>
    RosterMembersOnly = 0,

    /// <summary>
    /// Everything under the configured area path, whoever it is assigned to.
    /// </summary>
    /// <remarks>
    /// Appropriate only where the area path already isolates the squad. In a shared project
    /// it pulls in the client's work as well.
    /// </remarks>
    EveryoneInAreaPath = 1
}
