import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';

import { PictureCardComponent } from '../../components/picture-card/picture-card.component';
import { AstronomyService } from '../../services/astronomy.service';

/** Landing view backed by the API's UTC `today` endpoint. */
@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PictureCardComponent],
  template: `
    @if (loading()) {
      <section
        role="status"
        aria-live="polite"
        class="rounded-card border border-space-border bg-space-surface px-6 py-12 text-center"
      >
        <p class="text-body font-semibold text-content-primary">Connecting to the astronomy service...</p>
        <p class="mt-2 text-meta text-content-secondary">The first visit can take a moment.</p>
      </section>
    } @else if (error(); as requestError) {
      <section
        role="status"
        aria-live="polite"
        class="rounded-card border border-space-border bg-space-surface px-6 py-12 text-center"
      >
        <p class="text-body font-semibold text-content-primary">Today's picture is unavailable.</p>
        <p class="mt-2 text-meta text-content-secondary">{{ requestError.message }}</p>
        <button
          type="button"
          (click)="retry()"
          class="mt-6 rounded-button bg-accent px-5 py-2.5 text-meta font-semibold text-space-base transition hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          Retry
        </button>
      </section>
    } @else if (picture(); as entry) {
      <app-picture-card [entry]="entry" />
    }
  `
})
export class HomeComponent implements OnInit {
  private readonly astronomy = inject(AstronomyService);

  readonly picture = this.astronomy.currentPicture;
  readonly loading = this.astronomy.loading;
  readonly error = this.astronomy.error;

  ngOnInit(): void {
    this.astronomy.loadToday();
  }

  retry(): void {
    this.astronomy.loadToday();
  }
}
