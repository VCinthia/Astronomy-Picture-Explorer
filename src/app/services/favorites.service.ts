import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { normalizeReturnUrl } from '../auth/return-url';
import type { ApodEntry } from '../models/apod.model';
import { AuthService } from './auth.service';

export interface FavoriteRequestError {
  readonly code: string | null;
  readonly message: string;
}

/**
 * Owns the authenticated user's hydrated favorite collection.
 *
 * The service has deliberately no Web Storage fallback. A session boundary resets
 * every in-memory value before a new user can observe it, and a stale HTTP response
 * is ignored even if its browser cancellation reaches the server too late.
 */
@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly entriesState = signal<readonly ApodEntry[]>([]);
  private readonly pendingDatesState = signal<ReadonlySet<string>>(new Set());
  private readonly loadingState = signal(false);
  private readonly errorState = signal<FavoriteRequestError | null>(null);
  private readonly loadedState = signal(false);
  private readonly activeUserId = signal<string | null>(null);
  private sessionVersion = 0;
  private readonly locallyChangedDates = new Set<string>();
  private listSubscription: Subscription | null = null;
  private readonly mutationSubscriptions = new Map<string, Subscription>();

  /**
   * These public values fail closed immediately when AuthService changes users.
   * Angular effects then cancel/reset state, but rendering never waits for that
   * asynchronous scheduling boundary before hiding the previous account's data.
   */
  readonly entries = computed(() =>
    this.isActiveForCurrentUser() ? this.entriesState() : []
  );
  readonly pendingDates = computed(() =>
    this.isActiveForCurrentUser() ? this.pendingDatesState() : new Set<string>()
  );
  readonly loading = computed(() => this.isActiveForCurrentUser() && this.loadingState());
  readonly error = computed(() =>
    this.isActiveForCurrentUser() ? this.errorState() : null
  );
  readonly loaded = computed(() => this.isActiveForCurrentUser() && this.loadedState());

  constructor() {
    effect(() => {
      // The event is read explicitly as the public W9 contract. currentUser also
      // covers a service created after bootstrap has already restored a session.
      this.auth.sessionChange();
      this.activateUser(this.auth.currentUser()?.id ?? null);
    });
  }

  isFavorite(date: string): boolean {
    return this.isActiveForCurrentUser() && this.entriesState().some((entry) => entry.date === date);
  }

  isPending(date: string): boolean {
    return this.isActiveForCurrentUser() && this.pendingDatesState().has(date);
  }

  /** Retries the one collection request for the currently authenticated user. */
  retry(): void {
    const userId = this.synchronizeSession();
    if (userId !== null) {
      this.loadForUser(userId, true);
    }
  }

  /**
   * Adds or removes one date. The API remains the source of truth; local state
   * changes only after its idempotent 204 response and only for the active user.
   */
  toggle(entry: ApodEntry, currentUrl: string): void {
    const userId = this.synchronizeSession();
    if (userId === null) {
      this.redirectToLogin(currentUrl);
      return;
    }

    if (this.isPending(entry.date)) {
      return;
    }

    const wasFavorite = this.isFavorite(entry.date);
    const sessionVersion = this.sessionVersion;
    this.errorState.set(null);
    // Only protect mutations that race an in-flight collection response. Once
    // that response is reconciled the marker is cleared, so later reloads stay
    // authoritative instead of preserving obsolete local data indefinitely.
    if (this.listSubscription !== null) {
      this.locallyChangedDates.add(entry.date);
    }
    this.setPending(entry.date, true);

    const request = wasFavorite
      ? this.http.delete<void>(`/api/favorites/${encodeURIComponent(entry.date)}`)
      : this.http.post<void>('/api/favorites', { apod_date: entry.date });

    const subscription = request.subscribe({
      next: () => {
        if (!this.isCurrentUser(userId, sessionVersion)) {
          return;
        }

        this.entriesState.update((entries) =>
          wasFavorite
            ? entries.filter((favorite) => favorite.date !== entry.date)
            : entries.some((favorite) => favorite.date === entry.date)
              ? entries
              : [entry, ...entries].sort((left, right) => right.date.localeCompare(left.date))
        );
      },
      error: (error: unknown) => {
        if (this.isCurrentUser(userId, sessionVersion)) {
          this.locallyChangedDates.delete(entry.date);
          this.errorState.set(toFavoriteRequestError(error));
          this.setPending(entry.date, false);
          this.mutationSubscriptions.delete(entry.date);
        }
      },
      complete: () => {
        if (this.isCurrentUser(userId, sessionVersion)) {
          this.setPending(entry.date, false);
          this.mutationSubscriptions.delete(entry.date);
        }
      }
    });

    this.mutationSubscriptions.set(entry.date, subscription);
  }

  private activateUser(userId: string | null): void {
    if (userId === this.activeUserId()) {
      return;
    }

    this.sessionVersion += 1;
    this.listSubscription?.unsubscribe();
    this.listSubscription = null;
    for (const subscription of this.mutationSubscriptions.values()) {
      subscription.unsubscribe();
    }
    this.mutationSubscriptions.clear();
    this.activeUserId.set(userId);
    this.locallyChangedDates.clear();
    this.entriesState.set([]);
    this.pendingDatesState.set(new Set());
    this.loadingState.set(false);
    this.errorState.set(null);
    this.loadedState.set(false);

    if (userId !== null) {
      this.loadForUser(userId);
    }
  }

  private loadForUser(userId: string, force = false): void {
    if (!this.isCurrentUser(userId, this.sessionVersion) || (this.loadedState() && !force)) {
      return;
    }

    this.listSubscription?.unsubscribe();
    const sessionVersion = this.sessionVersion;
    this.loadingState.set(true);
    this.errorState.set(null);

    this.listSubscription = this.http.get<readonly ApodEntry[]>('/api/favorites').subscribe({
      next: (entries) => {
        if (!this.isCurrentUser(userId, sessionVersion)) {
          return;
        }

        // A click can finish while the initial GET is in flight. Preserve each
        // locally changed date so an older list response cannot undo that action.
        const locallyCurrent = this.entriesState().filter((entry) =>
          this.locallyChangedDates.has(entry.date)
        );
        const fromServer = entries.filter((entry) => !this.locallyChangedDates.has(entry.date));
        this.entriesState.set(
          [...fromServer, ...locallyCurrent].sort((left, right) => right.date.localeCompare(left.date))
        );
        this.locallyChangedDates.clear();
        this.loadedState.set(true);
      },
      error: (error: unknown) => {
        if (this.isCurrentUser(userId, sessionVersion)) {
          this.errorState.set(toFavoriteRequestError(error));
          this.loadingState.set(false);
          this.listSubscription = null;
        }
      },
      complete: () => {
        if (this.isCurrentUser(userId, sessionVersion)) {
          this.loadingState.set(false);
          this.listSubscription = null;
        }
      }
    });
  }

  private redirectToLogin(currentUrl: string): void {
    const returnUrl = normalizeReturnUrl(currentUrl) ?? '/home';
    void this.router.navigate(['/login'], { queryParams: { returnUrl } });
  }

  private setPending(date: string, pending: boolean): void {
    this.pendingDatesState.update((dates) => {
      const next = new Set(dates);
      if (pending) {
        next.add(date);
      } else {
        next.delete(date);
      }
      return next;
    });
  }

  private isCurrentUser(userId: string, sessionVersion: number): boolean {
    return (
      this.activeUserId() === userId &&
      this.auth.currentUser()?.id === userId &&
      this.sessionVersion === sessionVersion
    );
  }

  private isActiveForCurrentUser(): boolean {
    const activeUserId = this.activeUserId();
    return activeUserId !== null && this.auth.currentUser()?.id === activeUserId;
  }

  /**
   * Effects perform normal session cleanup, while user actions synchronize the
   * boundary immediately so a just-authenticated account is never redirected
   * as anonymous and an old account cannot keep accepting mutations.
   */
  private synchronizeSession(): string | null {
    const userId = this.auth.currentUser()?.id ?? null;
    if (userId !== this.activeUserId()) {
      this.activateUser(userId);
    }
    return userId;
  }
}

function toFavoriteRequestError(error: unknown): FavoriteRequestError {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { code?: unknown; detail?: unknown; title?: unknown } | null;
    return {
      code: typeof body?.code === 'string' ? body.code : null,
      message:
        typeof body?.detail === 'string'
          ? body.detail
          : typeof body?.title === 'string'
            ? body.title
            : 'Your favorites could not be updated. Try again.'
    };
  }

  return { code: null, message: 'Your favorites could not be updated. Try again.' };
}
