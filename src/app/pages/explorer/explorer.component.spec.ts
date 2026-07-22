import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import type { ApodEntry } from '../../models/apod.model';
import { ExplorerComponent } from './explorer.component';

const entry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  url: 'https://example.test/wr134.jpg',
  hdurl: null,
  thumbnail_url: null,
  copyright: null
};

describe('ExplorerComponent', () => {
  let fixture: ComponentFixture<ExplorerComponent>;
  let element: HTMLElement;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExplorerComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ExplorerComponent);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => http.verify({ ignoreCancelled: true }));

  it('renders the real-calendar control before Search and aligns their desktop baselines', () => {
    expect(element.querySelector('app-search-bar')).not.toBeNull();
    expect(element.querySelector('input[type="date"]')).not.toBeNull();
    expect(element.querySelector('[role="listbox"]')).toBeNull();

    const controls = element.querySelector('div.grid') as HTMLDivElement;
    expect(controls.classList).toContain('md:items-end');
    expect(controls.children[0].tagName.toLowerCase()).toBe('app-date-picker');
    expect(controls.children[1].tagName.toLowerCase()).toBe('app-search-bar');
  });

  it('loads the selected date over HTTP and renders the returned picture', () => {
    fixture.componentInstance.onSelect(entry.date);
    http.expectOne(`/api/apod/date/${entry.date}`).flush(entry);
    fixture.detectChanges();

    expect(element.querySelector('app-picture-card')).not.toBeNull();
    expect(element.textContent).toContain(entry.title);
  });

  it('switches to backend search results after the search bar debounce commits a query', () => {
    fixture.componentInstance.onSearch('nebula');
    const request = http.expectOne((candidate) => candidate.url === '/api/apod/search');
    request.flush([entry]);
    fixture.detectChanges();

    const heading = element.querySelector('#search-results-heading') as HTMLHeadingElement;
    expect(element.querySelector('app-picture-card')).toBeNull();
    expect(element.querySelector('app-picture-grid')).not.toBeNull();
    expect(heading.textContent?.trim()).toBe('Search results for nebula');
  });

  it('shows an accessible empty state for a successful empty search', () => {
    fixture.componentInstance.onSearch('no matches');
    http.expectOne((candidate) => candidate.url === '/api/apod/search').flush([]);
    fixture.detectChanges();

    const status = element.querySelector('[role="status"]');
    expect(status?.textContent).toContain('No pictures found.');
    expect(status?.getAttribute('aria-live')).toBe('polite');
  });

  it('explains catalog_not_ready and retries the same search', () => {
    fixture.componentInstance.onSearch('nebula');
    http.expectOne((candidate) => candidate.url === '/api/apod/search').flush(
      { code: 'catalog_not_ready', detail: 'Catalog is not ready.' },
      { status: 503, statusText: 'Service Unavailable' }
    );
    fixture.detectChanges();

    expect(element.textContent).toContain('The catalog is still being prepared.');
    const retryButton = Array.from(element.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Retry search')
    ) as HTMLButtonElement;
    retryButton.click();
    const retry = http.expectOne((candidate) => candidate.url === '/api/apod/search');
    expect(retry.request.params.get('q')).toBe('nebula');
  });

  it('clears and cancels search before loading a picked date', () => {
    fixture.componentInstance.onSearch('nebula');
    const search = http.expectOne((candidate) => candidate.url === '/api/apod/search');

    fixture.componentInstance.onSelect(entry.date);
    const date = http.expectOne(`/api/apod/date/${entry.date}`);

    expect(search.cancelled).toBeTrue();
    expect(fixture.componentInstance.searchQuery()).toBe('');
    date.flush(entry);
  });
});
