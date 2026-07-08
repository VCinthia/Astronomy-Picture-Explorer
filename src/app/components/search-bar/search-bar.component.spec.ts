import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';

import { SearchBarComponent } from './search-bar.component';

describe('SearchBarComponent', () => {
  let fixture: ComponentFixture<SearchBarComponent>;
  let element: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SearchBarComponent] }).compileComponents();

    fixture = TestBed.createComponent(SearchBarComponent);
    fixture.componentRef.setInput('query', '');
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  function input(): HTMLInputElement {
    return element.querySelector('input') as HTMLInputElement;
  }

  function type(value: string): void {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('labels the search input and uses inline SVG control icons', () => {
    expect(input().labels?.[0]?.textContent).toContain('Search astronomy pictures');
    expect(element.querySelectorAll('svg').length).toBe(1);

    type('mars');

    const clearButton = element.querySelector('button') as HTMLButtonElement;
    expect(clearButton.getAttribute('aria-label')).toBe('Clear search');
    expect(clearButton.querySelector('svg')).not.toBeNull();
  });

  it('creates a unique input and label relationship for every instance', () => {
    const secondFixture = TestBed.createComponent(SearchBarComponent);
    secondFixture.componentRef.setInput('query', '');
    secondFixture.detectChanges();

    const firstInput = input();
    const firstLabel = element.querySelector('label') as HTMLLabelElement;
    const secondElement = secondFixture.nativeElement as HTMLElement;
    const secondInput = secondElement.querySelector('input') as HTMLInputElement;
    const secondLabel = secondElement.querySelector('label') as HTMLLabelElement;

    expect(firstInput.id).toBeTruthy();
    expect(secondInput.id).toBeTruthy();
    expect(firstInput.id).not.toBe(secondInput.id);
    expect(firstLabel.htmlFor).toBe(firstInput.id);
    expect(secondLabel.htmlFor).toBe(secondInput.id);
  });

  it('emits the latest draft only after 300 ms', fakeAsync(() => {
    const emitted: string[] = [];
    fixture.componentInstance.queryChange.subscribe((query) => emitted.push(query));

    type('mar');
    tick(200);
    type('mars');
    tick(299);
    expect(emitted).toEqual([]);

    tick(1);
    expect(emitted).toEqual(['mars']);
  }));

  it('clears immediately and cancels a pending debounced emission', fakeAsync(() => {
    const emitted: string[] = [];
    fixture.componentInstance.queryChange.subscribe((query) => emitted.push(query));

    type('nebula');
    tick(200);
    (element.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(input().value).toBe('');
    expect(emitted).toEqual(['']);

    tick(100);
    expect(emitted).toEqual(['']);
  }));

  it('synchronizes its draft and cancels pending input when the parent query changes', fakeAsync(() => {
    const emitted: string[] = [];
    fixture.componentInstance.queryChange.subscribe((query) => emitted.push(query));

    type('pending query');
    fixture.componentRef.setInput('query', 'venus');
    fixture.detectChanges();
    tick(300);

    expect(input().value).toBe('venus');
    expect(emitted).toEqual([]);
  }));
});
