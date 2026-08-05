import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';

import { LoginComponent, normalizeReturnUrl } from './login.component';

describe('LoginComponent', () => {
  let http: HttpTestingController;
  let returnUrl: string | null;

  beforeEach(async () => {
    returnUrl = null;
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useFactory: () => ({
            snapshot: {
              queryParamMap: {
                get: (key: string) => key === 'returnUrl' ? returnUrl : null
              }
            }
          })
        }
      ]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('renders a generic credentials message for a 401 without surfacing backend detail', () => {
    const fixture = createValidLoginFixture();
    fixture.componentInstance.submit();
    const request = http.expectOne('/auth/login');
    request.flush({
      title: 'Invalid credentials.',
      detail: 'This internal detail must not be rendered.',
      status: 401,
      code: 'invalid_credentials'
    }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('We could not sign you in with those credentials.');
    expect(alert?.textContent).not.toContain('internal detail');
  });

  it('offers the password recovery route from the sign-in form', () => {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    const link = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .find((candidate) => candidate.textContent?.includes('Reset it'));
    expect(link?.getAttribute('href')).toBe('/forgot-password');
  });

  it('offers a resend CTA only for the email_unconfirmed 403 and sends its typed request', () => {
    const fixture = createValidLoginFixture();
    fixture.componentInstance.submit();
    const login = http.expectOne('/auth/login');
    login.flush({
      title: 'Email confirmation required.',
      detail: 'Confirm your email before signing in.',
      status: 403,
      code: 'email_unconfirmed'
    }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const resend = element.querySelector('button[type="button"]') as HTMLButtonElement;
    expect(resend.textContent).toContain('Resend confirmation email');
    resend.click();

    const resendRequest = http.expectOne('/auth/resend-confirmation');
    expect(resendRequest.request.method).toBe('POST');
    expect(resendRequest.request.body).toEqual({ email: 'astro@example.test' });
    resendRequest.flush({ message: 'If the address can receive a confirmation email, a message will be sent.' }, {
      status: 202,
      statusText: 'Accepted'
    });
    fixture.detectChanges();

    expect(element.querySelector('[role="status"]')?.textContent).toContain('confirmation email');
  });

  it('replaces the form with one short success message before redirecting home', fakeAsync(() => {
    localStorage.clear();
    sessionStorage.clear();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
    const fixture = createValidLoginFixture();
    fixture.componentInstance.submit();
    const request = http.expectOne('/auth/login');
    request.flush({
      accessToken: 'header.payload.signature',
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4', email: 'astro@example.test' }
    });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe('Signed in successfully.');
    expect(element.querySelector('form')).toBeNull();
    expect(element.querySelector('input')).toBeNull();
    expect(element.querySelector('section')?.getAttribute('aria-labelledby')).toBeNull();
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('accessToken')).toBeNull();
    tick(649);
    expect(navigate).not.toHaveBeenCalled();
    tick(1);
    expect(navigate).toHaveBeenCalledWith('/home');
  }));

  it('returns to an internal path after its success state and rejects unsafe values', fakeAsync(() => {
    returnUrl = '/favorites?view=cards';
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
    const fixture = createValidLoginFixture();
    fixture.componentInstance.submit();
    http.expectOne('/auth/login').flush({
      accessToken: 'header.payload.signature',
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4', email: 'astro@example.test' }
    });

    expect(navigate).not.toHaveBeenCalled();
    tick(650);
    expect(navigate).toHaveBeenCalledWith('/favorites?view=cards');
    expect(normalizeReturnUrl('//evil.example')).toBeNull();
    expect(normalizeReturnUrl('https://evil.example')).toBeNull();
    expect(normalizeReturnUrl('/%2f%2fevil.example')).toBeNull();
    expect(normalizeReturnUrl('/auth/refresh')).toBeNull();
  }));

  function createValidLoginFixture() {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.componentInstance.form.setValue({
      email: 'astro@example.test',
      password: 'Valid1!Password'
    });
    fixture.detectChanges();
    return fixture;
  }
});
