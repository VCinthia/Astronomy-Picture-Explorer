import { Component, ChangeDetectionStrategy } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AppComponent } from './app.component';
import { AuthService } from './services/auth.service';

@Component({
  changeDetection: ChangeDetectionStrategy.Default,
  template: '',
})
class TestPageComponent {}

const TEST_ROUTES = [
  { path: 'home', component: TestPageComponent },
  { path: 'explorer', component: TestPageComponent },
  { path: 'favorites', component: TestPageComponent },
  { path: 'favorites/detail', component: TestPageComponent },
  { path: 'login', component: TestPageComponent }
];

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(TEST_ROUTES)]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the brand and a router outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Astronomy Explorer');
    expect(compiled.querySelector('router-outlet')).not.toBeNull();
  });

  it('mounts mobile navigation and reserves its height only below md', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const layout = compiled.firstElementChild;

    expect(compiled.querySelector('app-bottom-nav')).not.toBeNull();
    expect(compiled.querySelector('app-bottom-nav nav')?.classList).toContain('md:hidden');
    expect(layout?.classList).toContain('pb-14');
    expect(layout?.classList).toContain('md:pb-0');
  });

  it('keeps date stepping out of the global header', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('header button[aria-label="Previous date"]')).toBeNull();
    expect(compiled.querySelector('header button[aria-label="Next date"]')).toBeNull();
  });

  it('uses SVGs instead of Unicode arrows for external links', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const portfolioLink = compiled.querySelector(`a[href] svg path[d="M7 17 17 7"]`);

    expect(portfolioLink).not.toBeNull();
    expect(compiled.textContent).not.toMatch(/[←→↗]/);
  });

  it('renders the desktop Home, Explore and Favorites destinations with visible focus', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav[aria-label="Primary"]');
    const links = Array.from(nav?.querySelectorAll('a') ?? []);

    expect(nav?.classList.contains('hidden')).toBeTrue();
    expect(nav?.classList.contains('md:flex')).toBeTrue();
    expect(nav?.classList.contains('sm:flex')).toBeFalse();
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      'Home',
      'Explore',
      'Favorites'
    ]);
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/home',
      '/explorer',
      '/favorites'
    ]);
    const indicators = links.map((link) => link.querySelector('span'));
    expect(links.every((link) => !link.classList.contains('border-b'))).toBeTrue();
    expect(links.every((link) => !link.classList.contains('rounded-button'))).toBeTrue();
    expect(indicators.every((indicator) => indicator?.classList.contains('border-b'))).toBeTrue();
    expect(links.every((link) => link.classList.contains('text-content-secondary'))).toBeTrue();
    expect(links.every((link) => link.classList.contains('focus-visible:outline-accent'))).toBeTrue();
  });

  it('keeps a visible, aligned Sign in entry in the header at every breakpoint', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const signIn = (fixture.nativeElement as HTMLElement).querySelector(
      'header a[href="/login"]'
    ) as HTMLAnchorElement;
    expect(signIn.textContent?.trim()).toBe('Sign in');
    expect(signIn.classList.contains('hidden')).toBeFalse();
    expect(signIn.classList.contains('rounded-button')).toBeFalse();
    expect(signIn.classList.contains('border-b')).toBeFalse();
    expect(signIn.querySelector('span')?.classList.contains('border-b')).toBeTrue();
    expect(signIn.classList.contains('text-content-secondary')).toBeTrue();
    expect(signIn.classList.contains('focus-visible:outline-accent')).toBeTrue();
  });

  it('replaces Sign in with a visible logout action and clears memory before its request settles', () => {
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    auth.login({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe();
    http.expectOne('/auth/login').flush({
      accessToken: 'access-token',
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4', email: 'astro@example.test' }
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const signOut = (fixture.nativeElement as HTMLElement).querySelector(
      'header button[type="button"]'
    ) as HTMLButtonElement;
    expect(signOut.textContent?.trim()).toBe('Sign out');
    signOut.click();

    expect(auth.isAuthenticated()).toBeFalse();
    http.expectOne('/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
  });

  it('keeps the compact mobile brand accessible when account entry shares the header', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const brand = (fixture.nativeElement as HTMLElement).querySelector(
      'header a[href="/home"]'
    ) as HTMLAnchorElement;
    expect(brand.getAttribute('aria-label')).toBe('Astronomy Explorer');
    expect(brand.querySelector('span.hidden.sm\\:inline')).not.toBeNull();
  });

  it('navigates to Favorites and exposes only its active state', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const element = fixture.nativeElement as HTMLElement;
    const favorites = element.querySelector('nav a[href="/favorites"]') as HTMLAnchorElement;

    favorites.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(router.url).toBe('/favorites');
    expect(favorites.getAttribute('aria-current')).toBe('page');
    const favoriteIndicator = favorites.querySelector('span') as HTMLSpanElement;
    expect(favoriteIndicator.classList.contains('text-accent')).toBeFalse();
    expect(favoriteIndicator.classList.contains('border-accent')).toBeTrue();
    expect(favorites.classList.contains('text-content-secondary')).toBeTrue();

    const inactiveLinks = element.querySelectorAll(
      'nav[aria-label="Primary"] a[href="/home"], nav[aria-label="Primary"] a[href="/explorer"]'
    );
    inactiveLinks.forEach((link) => {
      expect(link.getAttribute('aria-current')).toBeNull();
      const indicator = link.querySelector('span') as HTMLSpanElement;
      expect(indicator.classList.contains('text-accent')).toBeFalse();
      expect(indicator.classList.contains('border-transparent')).toBeTrue();
      expect(link.classList.contains('text-content-secondary')).toBeTrue();
    });
  });

  it('uses exact active matching for every desktop destination', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const element = fixture.nativeElement as HTMLElement;
    const destinations = ['/home', '/explorer', '/favorites'];

    for (const destination of destinations) {
      await router.navigateByUrl(destination);
      fixture.detectChanges();

      const links = Array.from(element.querySelectorAll('nav[aria-label="Primary"] a'));
      const activeLinks = links.filter((link) => link.getAttribute('aria-current') === 'page');

      expect(activeLinks.length).toBe(1);
      expect(activeLinks[0].getAttribute('href')).toBe(destination);
      const indicator = activeLinks[0].querySelector('span') as HTMLSpanElement;
      expect(indicator.classList.contains('text-accent')).toBeFalse();
      expect(indicator.classList.contains('border-accent')).toBeTrue();
      expect(activeLinks[0].classList.contains('text-content-secondary')).toBeTrue();
    }

    await router.navigateByUrl('/favorites/detail');
    fixture.detectChanges();

    const falselyActive = element.querySelectorAll(
      'nav[aria-label="Primary"] a[aria-current="page"]'
    );
    expect(falselyActive.length).toBe(0);

    await router.navigateByUrl('/favorites?view=cards');
    fixture.detectChanges();

    expect(element.querySelector('nav[aria-label="Primary"] a[href="/favorites"]')?.getAttribute('aria-current')).toBe('page');
  });

  it('marks Sign in for login with a favorites return URL', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const element = fixture.nativeElement as HTMLElement;

    await router.navigateByUrl('/login?returnUrl=%2Ffavorites');
    fixture.detectChanges();

    const signIn = element.querySelector('header a[href="/login"]') as HTMLAnchorElement;
    const signInIndicator = signIn.querySelector('span') as HTMLSpanElement;
    expect(signIn.getAttribute('aria-current')).toBe('page');
    expect(signInIndicator.classList.contains('border-accent')).toBeTrue();
    expect(signIn.classList.contains('text-content-secondary')).toBeTrue();
    expect(element.querySelectorAll('nav[aria-label="Primary"] a[aria-current="page"]').length).toBe(0);
  });
});
