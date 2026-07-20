import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { APOD_FIRST_DATE, AstronomyService, utcToday } from './services/astronomy.service';
import { AuthService } from './services/auth.service';
import { AUTHOR_NAME, AUTHOR_SITE_URL, SITE_CREATED } from './config/site.config';
import { BottomNavComponent } from './components/bottom-nav/bottom-nav.component';
import { formatApodDate } from './utils/format-date';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, BottomNavComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  private readonly astronomy = inject(AstronomyService);
  readonly auth = inject(AuthService);

  readonly authorName = AUTHOR_NAME;
  readonly authorSiteUrl = AUTHOR_SITE_URL;
  readonly siteCreated = SITE_CREATED;

  /** The valid requested calendar date; `selectedDate` remains response-confirmed in the service. */
  readonly activeDate = this.astronomy.requestedDate;
  readonly formattedDate = computed(() => formatApodDate(this.activeDate()));

  readonly canPrev = computed(() => this.activeDate() > APOD_FIRST_DATE);
  readonly canNext = computed(() => this.activeDate() < utcToday());

  /** Step to the chronologically previous (older) archive date. */
  prev(): void {
    if (this.canPrev()) {
      this.astronomy.selectDate(stepUtcDate(this.activeDate(), -1));
    }
  }

  /** Step to the chronologically next (newer) archive date. */
  next(): void {
    if (this.canNext()) {
      this.astronomy.selectDate(stepUtcDate(this.activeDate(), 1));
    }
  }

  logout(): void {
    this.auth.logout().subscribe();
  }
}

function stepUtcDate(date: string, days: number): string {
  const value = new Date(`${date}T00:00:00.000Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}
