import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';

import { AstronomyService } from '../../services/astronomy.service';
import { ExplorerComponent } from './explorer.component';

describe('ExplorerComponent', () => {
  let fixture: ComponentFixture<ExplorerComponent>;
  let element: HTMLElement;
  let astronomy: AstronomyService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ExplorerComponent] }).compileComponents();

    astronomy = TestBed.inject(AstronomyService);
    astronomy.searchQuery.set('');
    fixture = TestBed.createComponent(ExplorerComponent);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  it('always renders search and date controls', () => {
    expect(element.querySelector('app-search-bar')).not.toBeNull();
    expect(element.querySelector('app-date-picker')).not.toBeNull();
  });

  it('shows the selected-date hero while the search query is empty', () => {
    expect(element.querySelector('app-picture-card')).not.toBeNull();
    expect(element.querySelector('app-picture-grid')).toBeNull();
  });

  it('switches to the results grid for a matching search query', () => {
    astronomy.searchQuery.set('  nebula  ');
    fixture.detectChanges();

    const resultsSection = element.querySelector('section[aria-labelledby]') as HTMLElement;
    const headingId = resultsSection.getAttribute('aria-labelledby');
    const heading = element.querySelector(`#${headingId}`) as HTMLHeadingElement;

    expect(element.querySelector('app-picture-card')).toBeNull();
    expect(element.querySelector('app-picture-grid')).not.toBeNull();
    expect(element.querySelectorAll('article').length).toBeGreaterThan(0);
    expect(heading.tagName).toBe('H1');
    expect(heading.textContent?.trim()).toBe('Search results for nebula');
  });

  it('shows an accessible empty state when a search has no matches', () => {
    astronomy.searchQuery.set('no-match-in-the-archive');
    fixture.detectChanges();

    const status = element.querySelector('[role="status"]');
    expect(status?.textContent).toContain('No pictures found.');
    expect(status?.getAttribute('aria-live')).toBe('polite');
    expect(element.querySelector('app-picture-grid')).toBeNull();
  });

  it('returns to the selected-date hero when a date is picked', () => {
    astronomy.searchQuery.set('nebula');
    fixture.componentInstance.onSelect('2026-05-22');
    fixture.detectChanges();

    expect(astronomy.searchQuery()).toBe('');
    expect(astronomy.selectedDate()).toBe('2026-05-22');
    expect(element.querySelector('app-picture-card')).not.toBeNull();
  });

  it('cancels pending search input when a date is picked', fakeAsync(() => {
    const input = element.querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'nebula';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.componentInstance.onSelect('2026-05-22');
    fixture.detectChanges();
    tick(300);
    fixture.detectChanges();

    expect(astronomy.searchQuery()).toBe('');
    expect(input.value).toBe('');
    expect(element.querySelector('app-picture-card')).not.toBeNull();
  }));
});
