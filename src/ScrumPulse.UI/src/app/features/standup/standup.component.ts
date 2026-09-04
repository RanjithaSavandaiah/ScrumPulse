import { Component, computed, inject, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { DailyStandup } from '../../core/models/scrum.models';
import { StandupFeedComponent } from './components/standup-feed/standup-feed.component';
import { StandupTimerComponent } from './components/standup-timer/standup-timer.component';
import { LogStandupModalComponent } from './components/log-standup-modal/log-standup-modal.component';

@Component({
  selector: 'app-standup',
  standalone: true,
  imports: [CommonModule, StandupFeedComponent, StandupTimerComponent, LogStandupModalComponent],
  templateUrl: './standup.component.html',
  styleUrl: './standup.component.css'
})
export class StandupComponent implements OnDestroy {
  state = inject(ScrumStateService);

  showStandupModal = signal(false);
  selectedEditStandup = signal<DailyStandup | null>(null);

  timerSeconds = signal<number>(120);
  timerRunning = signal<boolean>(false);
  currentSpeakerIndex = signal<number>(0);
  private timerInterval: any = null;

  contributingMembers = computed(() => {
    return this.state.squadMembers().filter(m => {
      const role = (m.role || '').toLowerCase();
      return role !== 'scrummaster' && role !== 'cdl' && role !== 'sm';
    });
  });

  openCreateStandup(): void {
    this.selectedEditStandup.set(null);
    this.showStandupModal.set(true);
  }

  openEditStandup(standup: DailyStandup): void {
    this.selectedEditStandup.set(standup);
    this.showStandupModal.set(true);
  }

  toggleTimer(): void {
    if (this.timerRunning()) {
      this.pauseTimer();
    } else {
      this.startTimer();
    }
  }

  startTimer(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
    this.timerRunning.set(true);

    this.timerInterval = setInterval(() => {
      if (this.timerSeconds() > 0) {
        this.timerSeconds.update(s => s - 1);
      } else {
        this.nextSpeaker();
      }
    }, 1000);
  }

  pauseTimer(): void {
    this.timerRunning.set(false);
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }

  resetTimer(): void {
    this.pauseTimer();
    this.timerSeconds.set(120);
  }

  nextSpeaker(): void {
    this.resetTimer();
    const list = this.contributingMembers();
    if (list.length > 0) {
      this.currentSpeakerIndex.update(idx => (idx + 1) % list.length);
    }
  }

  selectSpeaker(index: number): void {
    this.currentSpeakerIndex.set(index);
    this.resetTimer();
  }

  onSaveStandup(standupData: { teamMemberId: string; yesterdaySummary: string; todayPlan: string; blockersText: string; moodScore: number }): void {
    const editItem = this.selectedEditStandup();
    if (editItem) {
      this.state.updateStandup(editItem.id, {
        ...standupData,
        sprintId: editItem.sprintId || this.state.activeSprint()?.id
      });
    } else {
      this.state.submitStandup({
        ...standupData,
        sprintId: this.state.activeSprint()?.id
      });
    }
    this.showStandupModal.set(false);
    this.selectedEditStandup.set(null);
  }

  onDeleteStandup(id: string): void {
    this.state.deleteStandup(id);
    this.showStandupModal.set(false);
    this.selectedEditStandup.set(null);
  }

  ngOnDestroy(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }
}
