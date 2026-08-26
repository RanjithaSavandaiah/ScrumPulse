import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from './features/navbar/navbar.component';
import { IconComponent } from './core/components/icon/icon.component';
import { ScrumStateService } from './core/services/scrum-state.service';
import { WorkItemsComponent } from './features/work-items/work-items.component';
import { PrMetricsComponent } from './features/pr-metrics/pr-metrics.component';
import { TeamRosterComponent } from './features/team-roster/team-roster.component';
import { AiCoachComponent } from './features/ai-coach/ai-coach.component';
import { BlockersComponent } from './features/blockers/blockers.component';
import { StandupComponent } from './features/standup/standup.component';
import { CapacityComponent } from './features/capacity/capacity.component';
import { ReviewsComponent } from './features/reviews/reviews.component';
import { RetroComponent } from './features/retro/retro.component';
import { KudosComponent } from './features/kudos/kudos.component';
import { TechHubComponent } from './features/tech-hub/tech-hub.component';
import { ExecutiveComponent } from './features/executive/executive.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    NavbarComponent,
    IconComponent,
    WorkItemsComponent,
    PrMetricsComponent,
    TeamRosterComponent,
    AiCoachComponent,
    BlockersComponent,
    StandupComponent,
    CapacityComponent,
    ReviewsComponent,
    RetroComponent,
    KudosComponent,
    TechHubComponent,
    ExecutiveComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  state = inject(ScrumStateService);
  activeTab = signal<string>('standup');
}
