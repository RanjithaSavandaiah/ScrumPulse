import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent, IconName } from '../../core/components/icon/icon.component';
import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { RoleType, TeamMember } from '../../core/models/scrum.models';
import { CORE_PIPES } from '../../core/pipes';
import { isLeadershipRole } from '../../core/utils/format-utils';

@Component({
  selector: 'app-team-roster',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ConfirmModalComponent, ...CORE_PIPES],
  templateUrl: './team-roster.component.html',
  styleUrl: './team-roster.component.css'
})
export class TeamRosterComponent {
  state = inject(ScrumStateService);

  showAddModal = signal(false);
  memberToDelete = signal<TeamMember | null>(null);

  developerCount = computed(() => this.state.squadMembers().filter(m => m.role === 'Developer').length);
  qaCount = computed(() => this.state.squadMembers().filter(m => m.role === 'QaEngineer').length);
  leadershipCount = computed(() => this.state.squadMembers().filter(m => isLeadershipRole(m.role)).length);

  unassignedOrOtherMembers = computed(() => {
    const current = this.state.currentTeam();
    if (!current) return [];
    return this.state.members().filter(m => m.teamId !== current.id);
  });

  newMember: {
    name: string;
    email: string;
    role: RoleType;
    location: string;
    timeZone: string;
    activeWipLimit: number;
    avatar: string;
    teamId: string;
  } = {
    name: '',
    email: '',
    role: 'Developer',
    location: 'Offshore',
    timeZone: 'Asia/Kolkata (IST)',
    activeWipLimit: 3,
    avatar: '',
    teamId: ''
  };

  roles: { value: RoleType; label: string; icon: IconName; desc: string }[] = [
    { value: 'Developer', label: 'Developer', icon: 'wrench', desc: 'Code implementation, PRs & unit tests' },
    { value: 'QaEngineer', label: 'QA Engineer', icon: 'shield-check', desc: 'Quality gates, test suites & staging verification' },
    { value: 'ScrumMaster', label: 'Scrum Master', icon: 'zap', desc: 'Sprint facilitator & velocity coaching' },
    { value: 'Cdl', label: 'CDL', icon: 'award', desc: 'Client Delivery Lead — delivery governance & mentoring' },
    { value: 'ProductOwner', label: 'Product Owner', icon: 'building', desc: 'Product ownership & business requirements' },
    { value: 'AgileCoach', label: 'Agile Coach', icon: 'target', desc: 'Agile maturity, scaling practices & team coaching' }
  ];

  openAddModal(): void {
    this.newMember = {
      name: '',
      email: '',
      role: 'Developer',
      location: 'Offshore',
      timeZone: 'Asia/Kolkata (IST)',
      activeWipLimit: 3,
      avatar: '',
      teamId: this.state.currentTeam()?.id || ''
    };
    this.showAddModal.set(true);
  }

  getAssignedCount(memberId: string): number {
    return this.state.workItems().filter(item => item.assigneeId === memberId).length;
  }

  getRoleBadgeColor(role: string): string {
    switch (role) {
      case 'ScrumMaster': return 'var(--accent-warning)';
      case 'Developer': return 'var(--accent-primary)';
      case 'QaEngineer': return 'var(--accent-success)';
      case 'Cdl': return 'var(--accent-purple)';
      case 'ProductOwner':
      case 'ClientStakeholder': return 'var(--accent-secondary)';
      case 'AgileCoach': return '#ec4899';
      default: return 'var(--text-secondary)';
    }
  }

  getSquadName(teamId?: string | null): string {
    if (!teamId) return 'Unassigned Pool';
    const cleanId = teamId.toLowerCase().trim();
    const squad = this.state.teams().find(t => t.id.toLowerCase().trim() === cleanId);
    return squad ? squad.name : (this.state.currentTeam()?.name || 'Unassigned Pool');
  }

  getMemberSquadId(member: TeamMember): string {
    if (!member.teamId) return '';
    const cleanId = member.teamId.toLowerCase().trim();
    const squad = this.state.teams().find(t => t.id.toLowerCase().trim() === cleanId);
    return squad ? squad.id : '';
  }

  isMemberInSquad(member: TeamMember, squadId: string): boolean {
    if (!member.teamId || !squadId) return false;
    return member.teamId.toLowerCase().trim() === squadId.toLowerCase().trim();
  }

  onLinkMember(memberId: string): void {
    if (!this.state.canEditOrDelete() || !memberId) return;
    const current = this.state.currentTeam();
    if (!current) return;
    this.state.assignMemberSquad(memberId, current.id).subscribe();
  }

  onLinkAllUnassigned(): void {
    if (!this.state.canEditOrDelete()) return;
    const current = this.state.currentTeam();
    if (!current) return;
    const toLink = this.unassignedOrOtherMembers();
    if (toLink.length === 0) return;

    forkJoin(toLink.map(m => this.state.assignMemberSquad(m.id, current.id))).subscribe();
  }

  onAssignSquad(memberId: string, event: Event): void {
    if (!this.state.canEditOrDelete()) return;
    const selectEl = event.target as HTMLSelectElement;
    const teamId = selectEl.value ? selectEl.value : null;
    this.state.assignMemberSquad(memberId, teamId).subscribe();
  }

  onAssignSquadId(memberId: string, targetTeamId: string | null): void {
    if (!this.state.canEditOrDelete()) return;
    const teamId = targetTeamId && targetTeamId.trim() ? targetTeamId.trim() : null;
    this.state.assignMemberSquad(memberId, teamId).subscribe();
  }

  onSaveMember(): void {
    if (!this.newMember.name.trim()) return;

    const initials = this.newMember.name
      .split(' ')
      .filter(Boolean)
      .map(part => part[0])
      .join('')
      .toUpperCase();

    const squadId = this.newMember.teamId || this.state.currentTeam()?.id || undefined;

    this.state.createTeamMember({
      name: this.newMember.name.trim(),
      email: this.newMember.email.trim() || `${this.newMember.name.toLowerCase().replace(/\s+/g, '.')}@scrumpulse.io`,
      role: this.newMember.role,
      location: this.newMember.location,
      timeZone: this.newMember.timeZone,
      activeWipLimit: this.newMember.activeWipLimit || 3,
      avatar: initials.slice(0, 2),
      teamId: squadId
    });

    this.newMember = {
      name: '',
      email: '',
      role: 'Developer',
      location: 'Offshore',
      timeZone: 'Asia/Kolkata (IST)',
      activeWipLimit: 3,
      avatar: '',
      teamId: ''
    };

    this.showAddModal.set(false);
  }

  onDeleteMember(member: TeamMember): void {
    this.memberToDelete.set(member);
  }

  onConfirmDeleteMember(): void {
    const member = this.memberToDelete();
    if (member) {
      this.state.deleteTeamMember(member.id);
      this.memberToDelete.set(null);
    }
  }

  onCancelDeleteMember(): void {
    this.memberToDelete.set(null);
  }
}
