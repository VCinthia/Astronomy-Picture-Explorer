import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { RegisterComponent } from './register.component';

describe('RegisterComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('validates required account fields before making a request', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(http.match('/auth/register')).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Enter a valid email address.');
  });

  it('shows the generic acceptance state after a successful registration request', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.componentInstance.form.setValue({
      email: 'astro@example.test',
      password: 'Valid1!Password'
    });
    fixture.detectChanges();

    fixture.componentInstance.submit();
    const request = http.expectOne('/auth/register');
    request.flush({ message: 'If the address can receive a confirmation email, a message will be sent.' }, {
      status: 202,
      statusText: 'Accepted'
    });
    fixture.detectChanges();

    const status = (fixture.nativeElement as HTMLElement).querySelector('[role="status"]');
    expect(status?.textContent).toContain('Check your inbox');
  });
});
