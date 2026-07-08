import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { ApodEntry } from '../../models/apod.model';
import { formatApodDate } from '../../utils/format-date';

/** Presentational compact-card grid shared by search results and favorites. */
@Component({
  selector: 'app-picture-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid grid-cols-1 gap-6 md:grid-cols-3">
      @for (entry of entries(); track entry.date) {
        <article class="overflow-hidden rounded-card border border-space-border bg-space-surface">
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
  readonly entries = input.required<readonly ApodEntry[]>();

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
