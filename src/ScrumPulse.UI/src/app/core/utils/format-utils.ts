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
      return 'CDL Lead';
    case '4':
    case 'ClientStakeholder':
      return 'Client / PO';
    default:
      return roleStr;
  }
}

/**
 * Maps Kudos badge keys or integers to friendly titles.
 */
export function getBadgeLabel(badge?: string | number | null): string {
  if (badge === null || badge === undefined) return 'Kudos';

  const b = String(badge).toLowerCase();
  if (b.includes('0') || b.includes('problemsolver') || b.includes('problem')) return 'Problem Solver';
  if (b.includes('1') || b.includes('teamplayer') || b.includes('team')) return 'Team Player';
  if (b.includes('2') || b.includes('innovator')) return 'Innovator';
  if (b.includes('3') || b.includes('lifesaver') || b.includes('life')) return 'Life Saver';
  if (b.includes('4') || b.includes('speedy') || b.includes('fast')) return 'Speed Demon';
  return String(badge);
}
