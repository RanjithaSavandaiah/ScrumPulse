import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { NavbarComponent } from '../navbar/navbar.component';
import { IconComponent } from '../../core/components/icon/icon.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { WorkItemsComponent } from '../work-items/work-items.component';
import { PrMetricsComponent } from '../pr-metrics/pr-metrics.component';
import { TeamRosterComponent } from '../team-roster/team-roster.component';
import { AiCoachComponent } from '../ai-coach/ai-coach.component';
import { BlockersComponent } from '../blockers/blockers.component';
import { StandupComponent } from '../standup/standup.component';
import { CapacityComponent } from '../capacity/capacity.component';
import { ReviewsComponent } from '../reviews/reviews.component';
import { RetroComponent } from '../retro/retro.component';
import { KudosComponent } from '../kudos/kudos.component';
import { TechHubComponent } from '../tech-hub/tech-hub.component';
import { ExecutiveComponent } from '../executive/executive.component';
import { TeamPerformanceComponent } from '../team-performance/team-performance.component';
import { FooterComponent } from '../../core/components/footer/footer.component';
import { AdBannerComponent } from '../../core/components/ad-banner/ad-banner.component';

@Component({
  selector: 'app-dashboard',
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
    ExecutiveComponent,
    TeamPerformanceComponent,
    FooterComponent,
    AdBannerComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  state = inject(ScrumStateService);
  private route = inject(ActivatedRoute);

  activeTab = signal<string>('standup');

  ngOnInit(): void {
    this.route.fragment.subscribe(fragment => {
      if (fragment) {
        this.activeTab.set(fragment);
      }
    });
  }
}
