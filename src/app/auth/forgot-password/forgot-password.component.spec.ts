import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ForgotPasswordComponent } from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('submits the generic request and replaces the form with the generic success state', () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    fixture.componentInstance.form.setValue({ email: 'astro@example.test' });
    fixture.componentInstance.submit();
    const request = http.expectOne('/auth/forgot-password');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'astro@example.test' });
    request.flush({ message: 'If the address can receive a password reset email, a message will be sent.' }, {
      status: 202,
      statusText: 'Accepted'
    });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role="status"]')?.textContent).toContain('password reset email');
    expect(element.querySelector('form')).toBeNull();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('does not request a reset for an invalid form and announces validation accessibly', () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    fixture.componentInstance.form.setValue({ email: '' });
    fixture.componentInstance.submit();
    fixture.detectChanges();

    http.expectNone('/auth/forgot-password');
    expect((fixture.nativeElement as HTMLElement).querySelector('#forgot-password-email-error'))
      .not.toBeNull();
  });

  it('renders a generic failure message without surfacing server details', () => {
    const fixture = TestBed.createComponent(ForgotPasswordComponent);
    fixture.componentInstance.form.setValue({ email: 'astro@example.test' });
    fixture.componentInstance.submit();
    http.expectOne('/auth/forgot-password').flush({
      title: 'Unexpected provider detail.',
      detail: 'This must not be rendered.'
    }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent ?? '';
    expect(text).toContain('could not request a password reset');
    expect(text).not.toContain('provider detail');
  });
});
