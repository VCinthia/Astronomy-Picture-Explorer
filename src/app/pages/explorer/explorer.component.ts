import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';

import { AstronomyService } from '../../services/astronomy.service';
import { DatePickerComponent } from '../../components/date-picker/date-picker.component';
import { PictureCardComponent } from '../../components/picture-card/picture-card.component';
import { PictureGridComponent } from '../../components/picture-grid/picture-grid.component';
import { SearchBarComponent } from '../../components/search-bar/search-bar.component';

/**
 * Archive explorer: pick any date present in the mock and see its entry. The
 * date picker and the card are both driven by the service's `selectedDate`.
 */
@Component({
  selector: 'app-explorer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePickerComponent, PictureCardComponent, PictureGridComponent, SearchBarComponent],
  template: `
    <div class="flex flex-col gap-8">
      <div class="grid grid-cols-1 gap-2 md:grid-cols-3 md:gap-6">
        <div class="md:col-span-2">
          <app-search-bar [query]="searchQuery()" (queryChange)="onSearch($event)" />
        </div>
        <app-date-picker
          [dates]="dates"
          [selected]="selectedDate()"
          (dateSelected)="onSelect($event)"
        />
      </div>

      @if (hasSearchQuery()) {
        @if (searchResults().length > 0) {
          <section aria-labelledby="search-results-heading">
            <h1 id="search-results-heading" class="sr-only">
              Search results for {{ normalizedSearchQuery() }}
            </h1>
            <app-picture-grid [entries]="searchResults()" />
          </section>
        } @else {
          <section
            role="status"
            aria-live="polite"
            class="rounded-card border border-space-border bg-space-surface px-6 py-10 text-center"
          >
            <h1 class="text-title font-semibold text-content-primary">No pictures found.</h1>
            <p class="mt-2 text-body text-content-secondary">
              Try a different title or description.
            </p>
          </section>
        }
      } @else {
        @if (picture(); as entry) {
          <app-picture-card [entry]="entry" />
        } @else {
          <p class="text-body text-content-secondary">No picture available for this date.</p>
        }
      }
    </div>
  `
})
export class ExplorerComponent {
  private readonly astronomy = inject(AstronomyService);
  private readonly searchBar = viewChild(SearchBarComponent);

  /** Available dates, newest first, for the picker. */
  readonly dates = [...this.astronomy.availableDates].reverse();
  readonly selectedDate = this.astronomy.selectedDate;
  readonly picture = this.astronomy.currentPicture;
  readonly searchQuery = this.astronomy.searchQuery;
  readonly searchResults = this.astronomy.searchResults;
  readonly normalizedSearchQuery = computed(() => this.searchQuery().trim());
  readonly hasSearchQuery = computed(() => this.normalizedSearchQuery().length > 0);

  onSearch(query: string): void {
    this.astronomy.searchQuery.set(query);
  }

  onSelect(date: string): void {
    this.astronomy.selectDate(date);
    this.searchBar()?.clear();
    this.astronomy.searchQuery.set('');
  }
}
