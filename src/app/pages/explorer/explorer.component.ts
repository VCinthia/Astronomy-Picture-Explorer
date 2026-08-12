import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';

import { DatePickerComponent } from '../../components/date-picker/date-picker.component';
import { PictureCardComponent } from '../../components/picture-card/picture-card.component';
import { PictureGridComponent } from '../../components/picture-grid/picture-grid.component';
import { SearchBarComponent } from '../../components/search-bar/search-bar.component';
import { AstronomyService } from '../../services/astronomy.service';

/** Explore one APOD date or query the prepared catalog without a bundled archive. */
@Component({
  selector: 'app-explorer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePickerComponent, PictureCardComponent, PictureGridComponent, SearchBarComponent],
  template: `
    <div class="flex flex-col gap-8">
      <div class="grid grid-cols-1 gap-6 md:grid-cols-[minmax(16rem,1fr)_minmax(0,2fr)] md:items-end">
        <app-date-picker [selected]="requestedDate()" (dateSelected)="onSelect($event)" />
        <app-search-bar [query]="searchQuery()" (queryChange)="onSearch($event)" />
      </div>

      @if (hasSearchQuery()) {
        @if (searchLoading()) {
          <section role="status" aria-live="polite" class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center">
            <p class="text-body font-semibold text-content-primary">Searching the astronomy catalog...</p>
          </section>
        } @else {
          @if (searchError(); as requestError) {
            <section role="status" aria-live="polite" class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center">
              @if (requestError.code === 'catalog_not_ready') {
                <h1 class="text-title font-semibold text-content-primary">The catalog is still being prepared.</h1>
                <p class="mt-2 text-body text-content-secondary">Try again later. Pictures by date are still available.</p>
              } @else {
                <h1 class="text-title font-semibold text-content-primary">Search is temporarily unavailable.</h1>
                <p class="mt-2 text-body text-content-secondary">{{ requestError.message }}</p>
              }
              <button
                type="button"
                (click)="retrySearch()"
                class="mt-6 rounded-button bg-accent px-5 py-2.5 text-meta font-semibold text-space-base transition hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                Retry search
              </button>
            </section>
          } @else if (searchResults().length > 0) {
            <section aria-labelledby="search-results-heading">
              <h1 id="search-results-heading" class="sr-only">
                Search results for {{ normalizedSearchQuery() }}
              </h1>
              <app-picture-grid [entries]="searchResults()" />
            </section>
          } @else {
            <section role="status" aria-live="polite" class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center">
              <h1 class="text-title font-semibold text-content-primary">No pictures found.</h1>
              <p class="mt-2 text-body text-content-secondary">Try a different title or description.</p>
            </section>
          }
        }
      } @else if (loading()) {
        <section role="status" aria-live="polite" class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center">
          <p class="text-body font-semibold text-content-primary">Loading this picture...</p>
        </section>
      } @else {
        @if (error(); as requestError) {
          <section role="status" aria-live="polite" class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center">
            <h1 class="text-title font-semibold text-content-primary">This picture is unavailable.</h1>
            <p class="mt-2 text-body text-content-secondary">{{ requestError.message }}</p>
            <button
              type="button"
              (click)="retryDate()"
              class="mt-6 rounded-button bg-accent px-5 py-2.5 text-meta font-semibold text-space-base transition hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              Retry
            </button>
          </section>
        } @else {
          @if (picture(); as entry) {
            <app-picture-card [entry]="entry" />
          } @else {
            <section role="status" aria-live="polite" class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center">
              <p class="text-body text-content-secondary">Choose a date to load its astronomy picture.</p>
            </section>
          }
        }
      }
    </div>
  `
})
export class ExplorerComponent {
  private readonly astronomy = inject(AstronomyService);
  private readonly searchBar = viewChild(SearchBarComponent);

  readonly requestedDate = this.astronomy.requestedDate;
  readonly picture = this.astronomy.currentPicture;
  readonly loading = this.astronomy.loading;
  readonly error = this.astronomy.error;
  readonly searchQuery = this.astronomy.searchQuery;
  readonly searchResults = this.astronomy.searchResults;
  readonly searchLoading = this.astronomy.searchLoading;
  readonly searchError = this.astronomy.searchError;
  readonly normalizedSearchQuery = computed(() => this.searchQuery().trim());
  readonly hasSearchQuery = computed(() => this.normalizedSearchQuery().length > 0);

  onSearch(query: string): void {
    this.astronomy.setSearchQuery(query);
  }

  onSelect(date: string): void {
    this.searchBar()?.clear();
    this.astronomy.selectDate(date);
  }

  retryDate(): void {
    this.astronomy.retrySelectedDate();
  }

  retrySearch(): void {
    this.astronomy.retrySearch();
  }
}
