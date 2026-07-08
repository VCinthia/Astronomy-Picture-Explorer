import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { routes } from '../../app.routes';
import { AstronomyService } from '../../services/astronomy.service';
import { FavoritesComponent } from './favorites.component';

const FAVORITES_STORAGE_KEY = 'ape.favorites.v1';

describe('FavoritesComponent', () => {
  beforeEach(async () => {
    localStorage.removeItem(FAVORITES_STORAGE_KEY);
    await TestBed.configureTestingModule({
      imports: [FavoritesComponent],
      providers: [provideRouter([])]
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

  it('lists saved entries in descending date order and updates after removal', () => {
    const service = TestBed.inject(AstronomyService);
    service.toggleFavorite('2026-05-22');
    service.toggleFavorite('2026-06-09');

    const fixture = TestBed.createComponent(FavoritesComponent);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const cards = element.querySelectorAll('article');

    expect(cards.length).toBe(2);
    expect(cards[0].textContent).toContain("Thor's Helmet");
    expect(cards[1].textContent).toContain('The Nebulous Realm of WR 134');

    (cards[0].querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    const remainingCards = element.querySelectorAll('article');
    expect(remainingCards.length).toBe(1);
    expect(remainingCards[0].textContent).toContain('The Nebulous Realm of WR 134');
  });
});
