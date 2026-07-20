import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { routes } from '../../app.routes';
import { AstronomyService } from '../../services/astronomy.service';
import { FavoritesComponent } from './favorites.component';

const FAVORITES_STORAGE_KEY = 'ape.favorites.v1';

describe('FavoritesComponent', () => {
  beforeEach(async () => {
    localStorage.removeItem(FAVORITES_STORAGE_KEY);
    await TestBed.configureTestingModule({
      imports: [FavoritesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();
  });

  it('is exposed by a lazy /favorites route', () => {
    const favoritesRoute = routes.find((route) => route.path === 'favorites');

    expect(favoritesRoute).toBeDefined();
    expect(favoritesRoute?.component).toBeUndefined();
    expect(favoritesRoute?.loadComponent).toBeDefined();
  });

  it('shows an empty state with a CTA to the explorer', () => {
    const fixture = TestBed.createComponent(FavoritesComponent);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const cta = element.querySelector('a') as HTMLAnchorElement;

    expect(element.textContent).toContain('No favorites saved yet.');
    expect(element.querySelector('app-picture-grid')).toBeNull();
    expect(cta.textContent).toContain('Explore pictures');
    expect(cta.getAttribute('href')).toBe('/explorer');
  });

  it('keeps P2 favorite dates as a narrow temporary facade until W11 hydrates them by API', () => {
    const service = TestBed.inject(AstronomyService);
    service.toggleFavorite('2026-05-22');
    service.toggleFavorite('2026-06-09');

    const fixture = TestBed.createComponent(FavoritesComponent);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(service.favorites()).toEqual(['2026-05-22', '2026-06-09']);
    expect(element.querySelectorAll('article').length).toBe(0);
    expect(element.textContent).toContain('No favorites saved yet.');
  });
});
