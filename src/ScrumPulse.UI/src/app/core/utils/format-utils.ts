/**
 * Formats a member or presenter name by removing parenthetical role tags, e.g. "Priya Sharma (Developer)" -> "Priya Sharma".
 */
export function cleanName(name?: string | null): string {
  if (!name) return '';
  return name.replace(/\s*\([^)]*\)/g, '').trim();
}

/**
 * Extracts a 2-letter uppercase initials string from a name (e.g. "Priya Sharma" -> "PS").
 */
export function getInitials(name?: string | null): string {
  if (!name) return '??';
  const cleaned = cleanName(name);
  if (!cleaned) return '??';
  const parts = cleaned.split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  return cleaned.slice(0, 2).toUpperCase();
}

/**
 * Maps system role keys / enum integers into friendly display labels.
 */
export function getRoleLabel(role?: string | number | null): string {
  if (role === null || role === undefined) return '';

  const roleStr = String(role);
  switch (roleStr) {
    case '0':
    case 'ScrumMaster':
      return 'Scrum Master';
    case '1':
    case 'Developer':
      return 'Developer';
    case '2':
    case 'QaEngineer':
      return 'QA Engineer';
    case '3':
    case 'Cdl':
      return 'CDL';
    case '4':
    case 'ProductOwner':
    case 'ClientStakeholder':
      return 'Product Owner';
    case '5':
    case 'AgileCoach':
      return 'Agile Coach';
    default:
      return roleStr;
  }
}

/**
 * Maps Kudos badge keys or integers to friendly titles matching BadgeType enum.
 */
export function getBadgeLabel(badge?: string | number | null): string {
  if (badge === null || badge === undefined) return 'Kudos';

  switch (badge) {
    case 0:
    case '0':
    case 'ProblemSolver':
      return 'Problem Solver';
    case 1:
    case '1':
    case 'TeamPlayer':
      return 'Team Player';
    case 2:
    case '2':
    case 'GoalCrusher':
      return 'Goal Crusher';
    case 3:
    case '3':
    case 'QualityGuardian':
      return 'Quality Guardian';
    case 4:
    case '4':
    case 'InnovationStar':
      return 'Innovation Star';
    case 5:
    case '5':
    case 'ClientShoutout':
      return 'Client Shoutout';
    case 'Innovator':
      return 'Innovator';
    case 'LifeSaver':
      return 'Life Saver';
    case 'Speedy':
      return 'Speed Demon';
    default:
      return String(badge);
  }
}

/**
 * Returns true if the given role represents an active engineering/delivery contributor (Developer or QA Engineer).
 */
export function isDeliveryRole(role?: string | null): boolean {
  if (!role) return false;
  const r = role.toLowerCase().trim();
  return r === 'developer' || r === 'qaengineer';
}

/**
 * Returns true if the given role represents a leadership/facilitation role (Scrum Master, CDL, Agile Coach, Product Owner).
 */
export function isLeadershipRole(role?: string | null): boolean {
  if (!role) return false;
  const r = role.toLowerCase().trim();
  return r === 'scrummaster' || r === 'cdl' || r === 'agilecoach' || r === 'productowner' || r === 'clientstakeholder';
}
