namespace ScrumPulse.Domain.Enums;

/// <summary>
/// Roles a squad member can hold. Persisted by numeric value, so existing numbers are fixed.
/// </summary>
/// <remarks>
/// ClientStakeholder previously shared the value 4 with ProductOwner, which made the two
/// indistinguishable at runtime: a stakeholder round-tripped through the database came back
/// as a product owner, and every role comparison treated them as the same person. It now has
/// its own value. The other members keep their original numbers so stored rows still map.
/// </remarks>
public enum RoleType
{
    ScrumMaster = 0,
    Developer = 1,
    QaEngineer = 2,
    Cdl = 3,
    ProductOwner = 4,
    AgileCoach = 5,
    ClientStakeholder = 6
}
