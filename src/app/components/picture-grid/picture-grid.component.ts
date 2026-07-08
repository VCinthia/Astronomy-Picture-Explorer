import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';

import type { ApodEntry } from '../../models/apod.model';
import { AstronomyService } from '../../services/astronomy.service';
import { formatApodDate } from '../../utils/format-date';

/** Compact-card grid shared by search results and favorites, with persisted favorite controls. */
@Component({
  selector: 'app-picture-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-1 gap-6 md:grid-cols-3">
      @for (entry of entries(); track entry.date) {
        <article
          class="relative overflow-hidden rounded-card border border-space-border bg-space-surface"
        >
          <a
            [href]="destinationUrl(entry)"
            target="_blank"
            rel="noopener noreferrer"
            class="group block focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            [attr.aria-label]="mediaLabel(entry)"
          >
            <div class="relative h-grid-media overflow-hidden bg-space-surface-hi">
              <img
                class="size-full object-cover transition group-hover:scale-105"
                [src]="previewUrl(entry)"
                [alt]="entry.explanation"
                loading="lazy"
                decoding="async"
              />
              @if (entry.media_type === 'video') {
                <span
                  class="absolute left-3 top-3 rounded-chip bg-accent/20 px-2.5 py-1 text-badge font-semibold uppercase tracking-widest text-accent"
                >
                  Video
                </span>
              }
              <span
                class="absolute bottom-3 left-3 rounded-chip bg-space-surface-hi px-2.5 py-1 text-caption text-content-tertiary"
              >
                {{ formattedDate(entry.date) }}
              </span>
            </div>
          </a>

          <button
            type="button"
            class="absolute right-3 top-3 z-10 flex size-9 items-center justify-center rounded-full bg-space-base text-nav transition hover:text-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            [class.text-accent]="isFavorite(entry.date)"
            [class.text-content-secondary]="!isFavorite(entry.date)"
            [attr.aria-pressed]="isFavorite(entry.date)"
            [attr.aria-label]="favoriteLabel(entry)"
            (click)="toggleFavorite(entry.date)"
          >
            <svg
              class="size-5"
              viewBox="0 0 24 24"
              [attr.fill]="isFavorite(entry.date) ? 'currentColor' : 'none'"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path
                d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78Z"
              />
            </svg>
          </button>

          <div class="p-5">
            <h2 class="line-clamp-2 text-body font-semibold text-content-primary">
              {{ entry.title }}
            </h2>
          </div>
        </article>
      }
    </div>
  `
})
export class PictureGridComponent {
  private readonly astronomy = inject(AstronomyService);

  readonly entries = input.required<readonly ApodEntry[]>();

  isFavorite(date: string): boolean {
    return this.astronomy.isFavorite(date);
  }

  toggleFavorite(date: string): void {
    this.astronomy.toggleFavorite(date);
  }

  favoriteLabel(entry: ApodEntry): string {
    return this.isFavorite(entry.date)
      ? `Remove ${entry.title} from favorites`
      : `Add ${entry.title} to favorites`;
  }

  previewUrl(entry: ApodEntry): string {
    return entry.media_type === 'video' ? (entry.thumbnail_url ?? entry.url) : entry.url;
  }

  destinationUrl(entry: ApodEntry): string {
    return entry.media_type === 'image' ? (entry.hdurl ?? entry.url) : entry.url;
  }

  mediaLabel(entry: ApodEntry): string {
    return entry.media_type === 'video'
      ? `Watch video: ${entry.title}`
      : `Open high-resolution image: ${entry.title}`;
  }

  formattedDate(date: string): string {
    return formatApodDate(date);
  }
}
