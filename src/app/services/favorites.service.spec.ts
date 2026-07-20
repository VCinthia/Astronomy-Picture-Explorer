import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import type { ApodEntry } from '../models/apod.model';
import { AuthSessionChange, AuthUser, AuthService } from './auth.service';
import { FavoritesService } from './favorites.service';

const firstEntry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  url: 'https://example.test/wr134.jpg',
  hdurl: 'https://example.test/wr134-hd.jpg',
  thumbnail_url: null,
  copyright: null
};

const laterEntry: ApodEntry = {
  ...firstEntry,
  date: '2026-06-09',
  title: 'A later favorite'
};

const alice: AuthUser = { id: 'alice', email: 'alice@example.test' };
const bob: AuthUser = { id: 'bob', email: 'bob@example.test' };

class AuthSessionStub {
  readonly currentUser = signal<AuthUser | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly sessionChange = signal<AuthSessionChange>({ previousUserId: null, currentUser: null });

  signIn(user: AuthUser): void {
    const previousUser = this.currentUser();
    this.currentUser.set(user);
    this.sessionChange.set({ previousUserId: previousUser?.id ?? null, currentUser: user });
  }

  signOut(): void {
    const previousUser = this.currentUser();
    this.currentUser.set(null);
    this.sessionChange.set({ previousUserId: previousUser?.id ?? null, currentUser: null });
  }
}

describe('FavoritesService', () => {
  let service: FavoritesService;
  let http: HttpTestingController;
  let auth: AuthSessionStub;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    auth = new AuthSessionStub();
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    Object.defineProperty(router, 'url', { value: '/home' });
    router.navigate.and.resolveTo(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router }
      ]
    });
    service = TestBed.inject(FavoritesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify({ ignoreCancelled: true }));

  it('loads one hydrated collection per authenticated user session without N+1 requests', () => {
    auth.signIn(alice);
    TestBed.flushEffects();

    const request = http.expectOne('/api/favorites');
    expect(request.request.method).toBe('GET');
    request.flush([firstEntry, laterEntry]);

    expect(service.entries().map((entry) => entry.date)).toEqual([laterEntry.date, firstEntry.date]);
    expect(http.match('/api/favorites').length).toBe(0);
    TestBed.flushEffects();
    expect(http.match('/api/favorites').length).toBe(0);
  });

  it('invalidates public signals read before effects, then exposes the hydrated next user', () => {
    // Read while anonymous so the computed values are cached before any session effect.
    expect(service.entries()).toEqual([]);
    expect(service.loading()).toBeFalse();

    auth.signIn(alice);
    expect(service.entries()).toEqual([]);
    expect(service.loading()).toBeFalse();
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([firstEntry]);
    expect(service.entries()).toEqual([firstEntry]);

    // Read again across A -> B before flushing. activeUserId is signal-backed, so
    // the previous cached collection cannot remain visible once B activates.
    auth.signIn(bob);
    expect(service.entries()).toEqual([]);
    expect(service.loading()).toBeFalse();

    TestBed.flushEffects();
    const bobRequest = http.expectOne('/api/favorites');
    expect(service.loading()).toBeTrue();
    bobRequest.flush([laterEntry]);

    expect(service.entries()).toEqual([laterEntry]);
    expect(service.loading()).toBeFalse();
  });

  it('uses the exact idempotent POST contract and blocks a duplicate toggle while pending', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([]);

    service.toggle(firstEntry, '/explorer?query=nebula');
    service.toggle(firstEntry, '/explorer?query=nebula');

    const request = http.expectOne('/api/favorites');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ apod_date: firstEntry.date });
    expect(service.isPending(firstEntry.date)).toBeTrue();
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.isPending(firstEntry.date)).toBeFalse();
    expect(service.isFavorite(firstEntry.date)).toBeTrue();
  });

  it('deletes the selected date with the exact protected endpoint', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([firstEntry]);

    service.toggle(firstEntry, '/favorites');
    const request = http.expectOne(`/api/favorites/${firstEntry.date}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.entries()).toEqual([]);
  });

  it('reconciles a later collection response instead of preserving a past local mutation', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([]);

    service.toggle(firstEntry, '/home');
    http.expectOne('/api/favorites').flush(null, { status: 204, statusText: 'No Content' });
    expect(service.entries()).toEqual([firstEntry]);

    service.retry();
    http.expectOne('/api/favorites').flush([laterEntry]);

    expect(service.entries()).toEqual([laterEntry]);
  });

  it('clears user-scoped memory and ignores a stale collection when the account changes', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    const aliceRequest = http.expectOne('/api/favorites');

    auth.signIn(bob);
    TestBed.flushEffects();
    expect(aliceRequest.cancelled).toBeTrue();
    expect(service.entries()).toEqual([]);

    http.expectOne('/api/favorites').flush([laterEntry]);
    expect(service.entries()).toEqual([laterEntry]);

    auth.signOut();
    TestBed.flushEffects();
    expect(service.entries()).toEqual([]);
    expect(service.loaded()).toBeFalse();
  });

  it('denies an old GET result immediately after A-to-B, before the session effect runs', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    const aliceRequest = http.expectOne('/api/favorites');

    auth.signIn(bob);
    aliceRequest.flush([firstEntry]);

    expect(service.entries()).toEqual([]);
    expect(service.isFavorite(firstEntry.date)).toBeFalse();

    TestBed.flushEffects();
    http.match('/api/favorites').forEach((request) => request.flush([laterEntry]));
  });

  it('denies an old POST result immediately after A-to-B, before the session effect runs', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([]);
    service.toggle(firstEntry, '/home');
    const addRequest = http.expectOne('/api/favorites');

    auth.signIn(bob);
    addRequest.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.entries()).toEqual([]);
    expect(service.isFavorite(firstEntry.date)).toBeFalse();

    TestBed.flushEffects();
    http.match('/api/favorites').forEach((request) => request.flush([laterEntry]));
  });

  it('denies an old DELETE result immediately after A-to-B, before the session effect runs', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([firstEntry]);
    service.toggle(firstEntry, '/favorites');
    const deleteRequest = http.expectOne(`/api/favorites/${firstEntry.date}`);

    auth.signIn(bob);
    deleteRequest.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.entries()).toEqual([]);
    expect(service.isFavorite(firstEntry.date)).toBeFalse();

    TestBed.flushEffects();
    http.match('/api/favorites').forEach((request) => request.flush([laterEntry]));
  });

  it('denies an old GET result immediately after logout, before the session effect runs', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    const request = http.expectOne('/api/favorites');

    auth.signOut();
    request.flush([firstEntry]);

    expect(service.entries()).toEqual([]);
    expect(service.loading()).toBeFalse();
  });

  it('denies an old POST result immediately after logout, before the session effect runs', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([]);
    service.toggle(firstEntry, '/home');
    const request = http.expectOne('/api/favorites');

    auth.signOut();
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.entries()).toEqual([]);
    expect(service.isFavorite(firstEntry.date)).toBeFalse();
  });

  it('denies an old DELETE result immediately after logout, before the session effect runs', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([firstEntry]);
    service.toggle(firstEntry, '/favorites');
    const request = http.expectOne(`/api/favorites/${firstEntry.date}`);

    auth.signOut();
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.entries()).toEqual([]);
    expect(service.isFavorite(firstEntry.date)).toBeFalse();
  });

  it('exposes a recoverable list failure and retries only for the active session', () => {
    auth.signIn(alice);
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush(
      { detail: 'The favorites service is temporarily unavailable.' },
      { status: 503, statusText: 'Unavailable' }
    );

    expect(service.error()?.message).toContain('temporarily unavailable');
    service.retry();
    http.expectOne('/api/favorites').flush([firstEntry]);

    expect(service.error()).toBeNull();
    expect(service.entries()).toEqual([firstEntry]);
  });

  it('sends an anonymous heart action to a normalized internal login return URL', () => {
    service.toggle(firstEntry, '//outside.example.test');

    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/home' }
    });
    expect(http.match(() => true)).toEqual([]);
  });
});
