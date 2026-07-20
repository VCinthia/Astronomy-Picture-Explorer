import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { routes } from '../../app.routes';
import type { ApodEntry } from '../../models/apod.model';
import { AuthSessionChange, AuthUser, AuthService } from '../../services/auth.service';
import { FavoritesService } from '../../services/favorites.service';
import { FavoritesComponent } from './favorites.component';

const entry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  url: 'https://example.test/wr134.jpg',
  hdurl: 'https://example.test/wr134-hd.jpg',
  thumbnail_url: null,
  copyright: null
};

class AuthSessionStub {
  readonly currentUser = signal<AuthUser | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly sessionChange = signal<AuthSessionChange>({ previousUserId: null, currentUser: null });

  signIn(): void {
    const user = { id: 'alice', email: 'alice@example.test' };
    this.currentUser.set(user);
    this.sessionChange.set({ previousUserId: null, currentUser: user });
  }
}

describe('FavoritesComponent', () => {
  let auth: AuthSessionStub;
  let http: HttpTestingController;

  beforeEach(async () => {
    auth = new AuthSessionStub();
    await TestBed.configureTestingModule({
      imports: [FavoritesComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify({ ignoreCancelled: true }));

  it('is exposed by a lazy protected /favorites route', () => {
    const favoritesRoute = routes.find((route) => route.path === 'favorites');

    expect(favoritesRoute).toBeDefined();
    expect(favoritesRoute?.component).toBeUndefined();
    expect(favoritesRoute?.loadComponent).toBeDefined();
    expect(favoritesRoute?.canActivate?.length).toBe(1);
  });

  it('shows a loading state then an API-hydrated collection without per-card requests', () => {
    const service = TestBed.inject(FavoritesService);
    auth.signIn();
    TestBed.flushEffects();
    const request = http.expectOne('/api/favorites');

    const fixture = TestBed.createComponent(FavoritesComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading your favorites...');

    request.flush([entry]);
    fixture.detectChanges();

    expect(service.entries()).toEqual([entry]);
    expect(fixture.nativeElement.querySelectorAll('article').length).toBe(1);
    expect(http.match('/api/favorites').length).toBe(0);
  });

  it('shows an empty state with a CTA to the explorer', () => {
    const service = TestBed.inject(FavoritesService);
    auth.signIn();
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([]);

    const fixture = TestBed.createComponent(FavoritesComponent);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const cta = element.querySelector('a') as HTMLAnchorElement;

    expect(service.entries()).toEqual([]);
    expect(element.textContent).toContain('No favorites saved yet.');
    expect(element.querySelector('app-picture-grid')).toBeNull();
    expect(cta.textContent).toContain('Explore pictures');
    expect(cta.getAttribute('href')).toBe('/explorer');
  });

  it('shows a retryable error state', () => {
    TestBed.inject(FavoritesService);
    auth.signIn();
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush(
      { detail: 'The API is waking up.' },
      { status: 503, statusText: 'Unavailable' }
    );

    const fixture = TestBed.createComponent(FavoritesComponent);
    fixture.detectChanges();

    const retry = (Array.from(
      fixture.nativeElement.querySelectorAll('button')
    ) as HTMLButtonElement[]).find((button) =>
      button.textContent?.includes('Retry')
    ) as HTMLButtonElement;
    expect(fixture.nativeElement.textContent).toContain('Your favorites are unavailable.');
    retry.click();
    http.expectOne('/api/favorites').flush([]);
  });
});
