import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import type { ApodEntry } from '../../models/apod.model';
import { PictureGridComponent } from '../../components/picture-grid/picture-grid.component';
import { AstronomyService } from '../../services/astronomy.service';

/** Displays the valid archive entries saved in the browser, newest first. */
@Component({
  selector: 'app-favorites',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PictureGridComponent, RouterLink],
  template: `
    <section aria-labelledby="favorites-title">
      <div class="mb-8">
        <p class="text-caption font-medium uppercase tracking-widest text-accent">Your archive</p>
        <h1 id="favorites-title" class="mt-2 text-title font-bold text-content-primary">
          Favorites
        </h1>
      </div>

      @if (entries().length > 0) {
        <app-picture-grid [entries]="entries()" />
      } @else {
        <div
          class="rounded-card border border-space-border bg-space-surface px-6 py-12 text-center"
        >
          <p class="text-body font-semibold text-content-primary">No favorites saved yet.</p>
          <p class="mt-2 text-meta text-content-secondary">
            Explore the archive and use the heart button to save pictures here.
          </p>
          <a
            routerLink="/explorer"
            class="mt-6 inline-flex rounded-button bg-accent px-5 py-2.5 text-meta font-semibold text-space-base transition hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            Explore pictures
          </a>
        </div>
      }
    </section>
  `
})
export class FavoritesComponent {
  private readonly astronomy = inject(AstronomyService);

  readonly entries = computed<readonly ApodEntry[]>(() =>
    this.astronomy
      .favorites()
      .map((date) => this.astronomy.getByDate(date))
      .filter((entry): entry is ApodEntry => entry !== undefined)
      .sort((left, right) => right.date.localeCompare(left.date))
  );
}
