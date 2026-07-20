import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, effect, signal } from '@angular/core';
import { EMPTY, Subject, catchError, map, of, switchMap } from 'rxjs';

import type { ApodEntry } from '../models/apod.model';

export const APOD_FIRST_DATE = '1995-06-16';
export const APOD_SEARCH_PAGE_SIZE = 12;

const FAVORITES_STORAGE_KEY = 'ape.favorites.v1';
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export interface ApodRequestError {
  readonly code: string | null;
  readonly message: string;
}

interface PictureRequest {
  readonly endpoint: string;
}

type PictureResult =
  | { readonly entry: ApodEntry }
  | { readonly error: ApodRequestError };

type SearchResult =
  | { readonly entries: readonly ApodEntry[] }
  | { readonly error: ApodRequestError };

/**
 * Browser state for the app-owned APOD HTTP endpoints.
 *
 * P3-W11 still replaces the temporary P2 favorite-date storage below. This
 * wave intentionally keeps that narrow compatibility seam while all APOD
 * content itself comes exclusively from the backend.
 */
@Injectable({ providedIn: 'root' })
export class AstronomyService {
  private readonly pictureRequests = new Subject<PictureRequest>();
  private readonly searchRequests = new Subject<string>();
  private readonly favoriteDates = signal<string[]>(this.readStoredFavorites());
  private readonly rememberedEntries = new Map<string, ApodEntry>();

  /** Date confirmed by the latest APOD response; absent until the first response arrives. */
  readonly selectedDate = signal<string | null>(null);
  /** Valid date currently requested by the user while its response is pending. */
  readonly requestedDate = signal(utcToday());
  readonly currentPicture = signal<ApodEntry | null>(null);
  readonly loading = signal(false);
  readonly error = signal<ApodRequestError | null>(null);

  readonly searchQuery = signal('');
  readonly searchResults = signal<readonly ApodEntry[]>([]);
  readonly searchLoading = signal(false);
  readonly searchError = signal<ApodRequestError | null>(null);

  /** Temporary P2 compatibility state. P3-W11 moves this to `/api/favorites`. */
  readonly favorites = this.favoriteDates.asReadonly();

  constructor(private readonly http: HttpClient) {
    this.pictureRequests
      .pipe(
        switchMap((request) => {
          if (!request.endpoint) {
            return EMPTY;
          }

          this.loading.set(true);
          this.error.set(null);
          this.currentPicture.set(null);

          return this.http.get<ApodEntry>(request.endpoint).pipe(
            map((entry): PictureResult => ({ entry: this.remember(entry) })),
            catchError((error: unknown) => of({ error: toRequestError(error) } as PictureResult))
          );
        })
      )
      .subscribe((result) => {
        this.loading.set(false);
        if ('error' in result) {
          this.error.set(result.error);
          return;
        }

        this.selectedDate.set(result.entry.date);
        this.requestedDate.set(result.entry.date);
        this.currentPicture.set(result.entry);
      });

    this.searchRequests
      .pipe(
        switchMap((query) => {
          if (!query) {
            this.searchLoading.set(false);
            this.searchError.set(null);
            this.searchResults.set([]);
            return EMPTY;
          }

          this.searchLoading.set(true);
          this.searchError.set(null);
          this.searchResults.set([]);
          const params = new HttpParams()
            .set('q', query)
            .set('page', '1')
            .set('pageSize', String(APOD_SEARCH_PAGE_SIZE));

          return this.http.get<readonly ApodEntry[]>('/api/apod/search', { params }).pipe(
            map(
              (entries): SearchResult => ({
                entries: entries.map((entry) => this.remember(entry))
              })
            ),
            catchError((error: unknown) => of({ error: toRequestError(error) } as SearchResult))
          );
        })
      )
      .subscribe((result) => {
        this.searchLoading.set(false);
        if ('error' in result) {
          this.searchError.set(result.error);
          return;
        }

        this.searchResults.set(result.entries);
      });

    effect(() => this.persistFavorites(this.favoriteDates()));
  }

  /** Loads the backend's UTC picture of the day. */
  loadToday(): void {
    this.pictureRequests.next({ endpoint: '/api/apod/today' });
  }

  /** Loads one valid calendar date and cancels an earlier in-flight date request. */
  selectDate(date: string): void {
    if (!isApodDate(date)) {
      // Send an empty request through switchMap solely to cancel any earlier
      // in-flight date/today request before publishing the validation state.
      this.pictureRequests.next({ endpoint: '' });
      this.loading.set(false);
      this.error.set({
        code: 'invalid_apod_date',
        message: `Choose a date from ${APOD_FIRST_DATE} through today.`
      });
      return;
    }

    this.requestedDate.set(date);
    this.pictureRequests.next({ endpoint: `/api/apod/date/${date}` });
  }

  retrySelectedDate(): void {
    this.selectDate(this.requestedDate());
  }

  /** Commits a debounced query from SearchBar and cancels any stale HTTP search. */
  setSearchQuery(query: string): void {
    this.searchQuery.set(query);
    this.searchRequests.next(query.trim());
  }

  retrySearch(): void {
    this.searchRequests.next(this.searchQuery().trim());
  }

  /**
   * Returns an entry remembered during this SPA lifetime. It is only retained
   * for the legacy P2 favorite view until W11 hydrates favorites from its API.
   */
  getByDate(date: string): ApodEntry | undefined {
    return this.rememberedEntries.get(date);
  }

  toggleFavorite(date: string): void {
    this.favoriteDates.update((dates) =>
      dates.includes(date) ? dates.filter((favorite) => favorite !== date) : [...dates, date]
    );
  }

  isFavorite(date: string): boolean {
    return this.favoriteDates().includes(date);
  }

  private remember(entry: ApodEntry): ApodEntry {
    this.rememberedEntries.set(entry.date, entry);
    return entry;
  }

  private readStoredFavorites(): string[] {
    try {
      if (typeof localStorage === 'undefined') {
        return [];
      }

      const storedValue = localStorage.getItem(FAVORITES_STORAGE_KEY);
      if (storedValue === null) {
        return [];
      }

      const parsedValue: unknown = JSON.parse(storedValue);
      return Array.isArray(parsedValue) && parsedValue.every((date) => typeof date === 'string')
        ? [...new Set(parsedValue)]
        : [];
    } catch {
      return [];
    }
  }

  private persistFavorites(dates: readonly string[]): void {
    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(FAVORITES_STORAGE_KEY, JSON.stringify(dates));
      }
    } catch {
      // W11 makes this browser-only compatibility persistence obsolete.
    }
  }
}

export function utcToday(): string {
  return new Date().toISOString().slice(0, 10);
}

export function isApodDate(value: string): boolean {
  if (!DATE_PATTERN.test(value) || value < APOD_FIRST_DATE || value > utcToday()) {
    return false;
  }

  return new Date(`${value}T00:00:00.000Z`).toISOString().slice(0, 10) === value;
}

function toRequestError(error: unknown): ApodRequestError {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { code?: unknown; detail?: unknown; title?: unknown } | null;
    const code = typeof body?.code === 'string' ? body.code : null;
    const message =
      typeof body?.detail === 'string'
        ? body.detail
        : typeof body?.title === 'string'
          ? body.title
          : 'The astronomy service is unavailable. Try again.';
    return { code, message };
  }

  return { code: null, message: 'The astronomy service is unavailable. Try again.' };
}
