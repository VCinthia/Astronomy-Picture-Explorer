import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';

import { PictureCardComponent } from '../../components/picture-card/picture-card.component';
import { APOD_FIRST_DATE, AstronomyService, utcToday } from '../../services/astronomy.service';
import { formatApodDate } from '../../utils/format-date';

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
    } @else {
      @if (error(); as requestError) {
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
      } @else {
        @if (picture(); as entry) {
          <app-picture-card [entry]="entry">
            <div
              picture-card-overlay
              class="absolute bottom-4 right-4 z-40 flex items-center gap-2 rounded-button border border-space-border bg-space-base/90 p-1.5 shadow-lg backdrop-blur"
              role="group"
              aria-label="Step through dates"
            >
              <button
                type="button"
                (click)="prev()"
                [disabled]="!canPrev()"
                aria-label="Previous date"
                class="flex size-9 items-center justify-center rounded-button text-nav text-content-secondary transition hover:text-content-primary disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                <svg
                  class="size-4"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  aria-hidden="true"
                >
                  <path d="m15 18-6-6 6-6" />
                </svg>
              </button>
              <span
                class="min-w-24 text-center text-meta font-medium text-content-primary"
                aria-live="polite"
              >
                {{ formattedDate() }}
              </span>
              <button
                type="button"
                (click)="next()"
                [disabled]="!canNext()"
                aria-label="Next date"
                class="flex size-9 items-center justify-center rounded-button text-nav text-content-secondary transition hover:text-content-primary disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                <svg
                  class="size-4"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  aria-hidden="true"
                >
                  <path d="m9 18 6-6-6-6" />
                </svg>
              </button>
            </div>
          </app-picture-card>
        }
      }
    }
  `
})
export class HomeComponent implements OnInit {
  private readonly astronomy = inject(AstronomyService);

  readonly picture = this.astronomy.currentPicture;
  readonly loading = this.astronomy.loading;
  readonly error = this.astronomy.error;
  readonly activeDate = this.astronomy.requestedDate;
  readonly formattedDate = computed(() => formatApodDate(this.activeDate()));
  readonly canPrev = computed(() => this.activeDate() > APOD_FIRST_DATE);
  readonly canNext = computed(() => this.activeDate() < utcToday());

  ngOnInit(): void {
    this.astronomy.loadToday();
  }

  retry(): void {
    this.astronomy.loadToday();
  }

  prev(): void {
    if (this.canPrev()) {
      this.astronomy.selectDate(stepUtcDate(this.activeDate(), -1));
    }
  }

  next(): void {
    if (this.canNext()) {
      this.astronomy.selectDate(stepUtcDate(this.activeDate(), 1));
    }
  }
}

function stepUtcDate(date: string, days: number): string {
  const value = new Date(`${date}T00:00:00.000Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}
