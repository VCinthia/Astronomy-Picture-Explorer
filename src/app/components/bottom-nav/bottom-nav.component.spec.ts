import { Component, ChangeDetectionStrategy } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { BottomNavComponent } from './bottom-nav.component';

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  template: '',
})
class TestPageComponent {}

const TEST_ROUTES = [
  { path: 'home', component: TestPageComponent },
  { path: 'explorer', component: TestPageComponent },
  { path: 'favorites', component: TestPageComponent },
  { path: 'favorites/detail', component: TestPageComponent }
];

describe('BottomNavComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BottomNavComponent],
      providers: [provideRouter(TEST_ROUTES)]
    }).compileComponents();
  });

  it('renders an accessible mobile-only fixed navigation with three SVG destinations', () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const nav = element.querySelector('nav');
    const links = Array.from(element.querySelectorAll('a'));

    expect(nav?.getAttribute('aria-label')).toBe('Mobile primary');
    expect(nav?.classList).toContain('fixed');
    expect(nav?.classList).toContain('bottom-0');
    expect(nav?.classList).toContain('z-50');
    expect(nav?.classList).toContain('h-14');
    expect(nav?.classList).toContain('bg-space-surface');
    expect(nav?.classList).toContain('border-space-border');
    expect(nav?.classList).toContain('md:hidden');
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
    expect(links.every((link) => link.querySelector('svg[aria-hidden="true"]'))).toBeTrue();
    expect(links.every((link) => link.classList.contains('focus-visible:outline-accent'))).toBeTrue();
    expect(links.every((link) => link.classList.contains('border-b-2'))).toBeTrue();
    expect(element.textContent).not.toMatch(/[⌂◎♥♡🔍📅←→↗]/);
  });

  it('navigates through all destinations and exposes one mutually exclusive active state', async () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const element = fixture.nativeElement as HTMLElement;
    const destinations = ['/home', '/explorer', '/favorites'];

    for (const destination of destinations) {
      const link = element.querySelector(`a[href="${destination}"]`) as HTMLAnchorElement;
      link.click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(router.url).toBe(destination);
      const links = Array.from(element.querySelectorAll('a'));
      const activeLinks = links.filter((candidate) => candidate.getAttribute('aria-current') === 'page');

      expect(activeLinks.length).toBe(1);
      expect(activeLinks[0]).toBe(link);
      expect(link.classList).toContain('text-accent');
      expect(link.classList).not.toContain('text-content-secondary');
      expect(link.classList).toContain('border-accent');
      links
        .filter((candidate) => candidate !== link)
        .forEach((inactiveLink) => {
          expect(inactiveLink.getAttribute('aria-current')).toBeNull();
          expect(inactiveLink.classList).toContain('text-content-secondary');
          expect(inactiveLink.classList).not.toContain('text-accent');
          expect(inactiveLink.classList).toContain('border-transparent');
        });
    }
  });

  it('uses exact matching so nested routes do not select a parent tab', async () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/favorites/detail');
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelectorAll('a[aria-current="page"]').length
    ).toBe(0);

    await router.navigateByUrl('/favorites?view=cards');
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('a[href="/favorites"]')?.getAttribute('aria-current')
    ).toBe('page');
  });
});
