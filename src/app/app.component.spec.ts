import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AppComponent } from './app.component';

@Component({ template: '' })
class TestPageComponent {}

const TEST_ROUTES = [
  { path: '', component: TestPageComponent },
  { path: 'explorer', component: TestPageComponent },
  { path: 'favorites', component: TestPageComponent },
  { path: 'favorites/detail', component: TestPageComponent }
];

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter(TEST_ROUTES)]
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

  it('shows the selected date in the header stepper', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    // Default date resolves to an entry in the mock and renders as "Mon DD, YYYY".
    expect(compiled.textContent).toMatch(/[A-Z][a-z]{2} \d{2}, \d{4}/);
    expect(compiled.querySelector('button[aria-label="Previous date"]')).not.toBeNull();
    expect(compiled.querySelector('button[aria-label="Next date"]')).not.toBeNull();
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
      '/',
      '/explorer',
      '/favorites'
    ]);
    expect(links.every((link) => link.classList.contains('focus-visible:outline-accent'))).toBeTrue();
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
    expect(favorites.classList.contains('text-accent')).toBeTrue();
    expect(favorites.classList.contains('text-content-secondary')).toBeFalse();

    const inactiveLinks = element.querySelectorAll(
      'nav a[href="/"], nav a[href="/explorer"]'
    );
    inactiveLinks.forEach((link) => {
      expect(link.getAttribute('aria-current')).toBeNull();
      expect(link.classList.contains('text-accent')).toBeFalse();
      expect(link.classList.contains('text-content-secondary')).toBeTrue();
    });
  });

  it('uses exact active matching for every desktop destination', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const element = fixture.nativeElement as HTMLElement;
    const destinations = ['/', '/explorer', '/favorites'];

    for (const destination of destinations) {
      await router.navigateByUrl(destination);
      fixture.detectChanges();

      const links = Array.from(element.querySelectorAll('nav[aria-label="Primary"] a'));
      const activeLinks = links.filter((link) => link.getAttribute('aria-current') === 'page');

      expect(activeLinks.length).toBe(1);
      expect(activeLinks[0].getAttribute('href')).toBe(destination);
      expect(activeLinks[0].classList.contains('text-accent')).toBeTrue();
      expect(activeLinks[0].classList.contains('text-content-secondary')).toBeFalse();
    }

    await router.navigateByUrl('/favorites/detail');
    fixture.detectChanges();

    const falselyActive = element.querySelectorAll(
      'nav[aria-label="Primary"] a[aria-current="page"]'
    );
    expect(falselyActive.length).toBe(0);
  });
});
