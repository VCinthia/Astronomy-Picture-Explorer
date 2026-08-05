import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { ResetPasswordComponent } from './reset-password.component';

@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  template: ''
})
class LoginRouteComponent {}

describe('ResetPasswordComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('rejects an invalid capability before it can call the backend', async () => {
    const { fixture, http } = await createFixture({
      userId: 'not-a-guid',
      code: 'not+base64'
    });
    fixture.detectChanges();

    http.expectNone('/auth/reset-password');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent)
      .toContain('invalid or incomplete');
  });

  it('scrubs a valid capability before posting and navigates to login without auto-login', async () => {
    localStorage.clear();
    sessionStorage.clear();
    const { fixture, http, location } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'UmVzZXQtdG9rZW4'
    });
    const replaceState = spyOn(location, 'replaceState').and.callThrough();
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      password: 'New2!Password',
      passwordConfirmation: 'New2!Password'
    });
    fixture.componentInstance.submit();
    const request = http.expectOne('/auth/reset-password');

    expect(replaceState).toHaveBeenCalledWith('/reset-password');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'UmVzZXQtdG9rZW4',
      password: 'New2!Password'
    });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(location.path()).toBe('/login');
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('does not send mismatched passwords', async () => {
    const { fixture, http } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'UmVzZXQtdG9rZW4'
    });
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      password: 'New2!Password',
      passwordConfirmation: 'Other2!Password'
    });
    fixture.componentInstance.submit();
    fixture.detectChanges();

    http.expectNone('/auth/reset-password');
    expect((fixture.nativeElement as HTMLElement).querySelector('#reset-password-confirm-error'))
      .not.toBeNull();
  });

  it('discards the capability and replaces the form with a generic error after a failed reset', async () => {
    const { fixture, http, location } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'UmVzZXQtdG9rZW4'
    });
    const replaceState = spyOn(location, 'replaceState').and.callThrough();
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({
      password: 'New2!Password',
      passwordConfirmation: 'New2!Password'
    });
    fixture.componentInstance.submit();
    http.expectOne('/auth/reset-password').flush({
      title: 'Unable to reset password.',
      detail: 'Do not render this implementation detail.'
    }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(replaceState).toHaveBeenCalledWith('/reset-password');
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('could not reset this password');
    expect(element.querySelector('[role="alert"]')?.textContent).not.toContain('implementation detail');
    expect(element.querySelector('form')).toBeNull();
    fixture.componentInstance.submit();
    http.expectNone('/auth/reset-password');
  });

  async function createFixture(query: Record<string, string>) {
    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideLocationMocks(),
        provideRouter([{ path: 'login', component: LoginRouteComponent }]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(query) } }
        }
      ]
    }).compileComponents();

    return {
      fixture: TestBed.createComponent(ResetPasswordComponent),
      http: TestBed.inject(HttpTestingController),
      location: TestBed.inject(Location)
    };
  }
});
