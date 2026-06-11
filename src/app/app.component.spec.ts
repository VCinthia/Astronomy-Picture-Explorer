import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])]
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
});
