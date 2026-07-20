import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PictureGridComponent } from '../../components/picture-grid/picture-grid.component';
import { FavoritesService } from '../../services/favorites.service';

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

      @if (loading()) {
        <section
          role="status"
          aria-live="polite"
          class="rounded-card border border-space-border bg-space-surface px-6 py-12 text-center"
        >
          <p class="text-body font-semibold text-content-primary">Loading your favorites...</p>
        </section>
      } @else if (error(); as requestError) {
        <section
          role="alert"
          class="rounded-card border border-red-400/60 bg-red-400/10 px-6 py-12 text-center"
        >
          <p class="text-body font-semibold text-content-primary">Your favorites are unavailable.</p>
          <p class="mt-2 text-meta text-content-secondary">{{ requestError.message }}</p>
          <button
            type="button"
            (click)="retry()"
            class="mt-6 rounded-button bg-accent px-5 py-2.5 text-meta font-semibold text-space-base transition hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            Retry
          </button>
        </section>
      } @else if (entries().length > 0) {
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
  private readonly favorites = inject(FavoritesService);

  readonly entries = this.favorites.entries;
  readonly loading = this.favorites.loading;
  readonly error = this.favorites.error;

  retry(): void {
    this.favorites.retry();
  }
}
