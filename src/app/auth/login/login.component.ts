import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { normalizeReturnUrl } from '../return-url';
import { AuthProblem, AuthService } from '../../services/auth.service';

export { normalizeReturnUrl } from '../return-url';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="mx-auto max-w-md" aria-labelledby="login-title">
      <p class="text-caption font-medium uppercase tracking-widest text-accent">Your account</p>
      <h1 id="login-title" class="mt-2 text-title font-bold text-content-primary">Sign in</h1>
      <p class="mt-3 text-body text-content-secondary">Continue to your astronomy favorites.</p>
      @if (confirmationSuccess()) {
        <p role="status" aria-live="polite" class="mt-5 rounded-button border border-accent/50 bg-accent/10 px-4 py-3 text-meta text-content-primary">
          Your email has been confirmed. You can now sign in.
        </p>
      }

      <form class="mt-8 space-y-5" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div>
          <label for="login-email" class="block text-meta font-medium text-content-primary">Email</label>
          <input
            id="login-email"
            type="email"
            autocomplete="email"
            maxlength="256"
            formControlName="email"
            [attr.aria-invalid]="email.invalid && (email.touched || submitted())"
            aria-describedby="login-email-error"
            class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          @if (email.invalid && (email.touched || submitted())) {
            <p id="login-email-error" class="mt-2 text-meta text-red-300">Enter a valid email address.</p>
          }
        </div>

        <div>
          <label for="login-password" class="block text-meta font-medium text-content-primary">Password</label>
          <input
            id="login-password"
            type="password"
            autocomplete="current-password"
            formControlName="password"
            [attr.aria-invalid]="password.invalid && (password.touched || submitted())"
            aria-describedby="login-password-error"
            class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          @if (password.invalid && (password.touched || submitted())) {
            <p id="login-password-error" class="mt-2 text-meta text-red-300">Enter your password.</p>
          }
        </div>

        @if (loginError() === 'credentials') {
          <p role="alert" class="rounded-button border border-red-400/60 bg-red-400/10 px-4 py-3 text-meta text-red-100">
            We could not sign you in with those credentials.
          </p>
        }
        @if (loginError() === 'unconfirmed') {
          <div role="alert" class="rounded-button border border-amber-300/60 bg-amber-300/10 px-4 py-3 text-meta text-amber-50">
            <p>Confirm your email before signing in.</p>
            <button
              type="button"
              (click)="resendConfirmation()"
              [disabled]="auth.loading() || email.invalid"
              class="mt-3 font-semibold text-content-primary underline underline-offset-2 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              Resend confirmation email
            </button>
          </div>
        }
        @if (resendMessage()) {
          <p role="status" aria-live="polite" class="rounded-button border border-accent/50 bg-accent/10 px-4 py-3 text-meta text-content-primary">
            {{ resendMessage() }}
          </p>
        }
        @if (resendError()) {
          <p role="alert" class="rounded-button border border-red-400/60 bg-red-400/10 px-4 py-3 text-meta text-red-100">
            {{ resendError() }}
          </p>
        }
        @if (signedIn()) {
          <p role="status" aria-live="polite" class="rounded-button border border-accent/50 bg-accent/10 px-4 py-3 text-meta text-content-primary">
            Signed in successfully. Your session will stay available while this page is open.
          </p>
        }

        <button
          type="submit"
          [disabled]="auth.loading()"
          class="inline-flex w-full justify-center rounded-button bg-accent px-5 py-3 text-meta font-semibold text-space-base transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          {{ auth.loading() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <p class="mt-6 text-meta text-content-secondary">
        Need an account?
        <a routerLink="/register" class="font-medium text-accent underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Create one</a>.
      </p>
    </section>
  `
})
export class LoginComponent {
  readonly auth = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly location = inject(Location);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly confirmationSuccess = signal(hasConfirmationSuccess(this.location.getState()));
  readonly submitted = signal(false);
  readonly loginError = signal<'credentials' | 'unconfirmed' | null>(null);
  readonly resendMessage = signal<string | null>(null);
  readonly resendError = signal<string | null>(null);
  readonly signedIn = signal(false);
  readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', Validators.required]
  });

  get email() {
    return this.form.controls.email;
  }

  get password() {
    return this.form.controls.password;
  }

  submit(): void {
    this.submitted.set(true);
    this.loginError.set(null);
    this.resendMessage.set(null);
    this.resendError.set(null);
    this.signedIn.set(false);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const returnUrl = normalizeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
        if (returnUrl !== null) {
          void this.router.navigateByUrl(returnUrl);
          return;
        }

        this.signedIn.set(true);
      },
      error: (problem: AuthProblem) => {
        this.loginError.set(
          problem.status === 403 && problem.code === 'email_unconfirmed'
            ? 'unconfirmed'
            : 'credentials'
        );
      }
    });
  }

  resendConfirmation(): void {
    if (this.email.invalid) {
      this.email.markAsTouched();
      return;
    }

    this.resendMessage.set(null);
    this.resendError.set(null);
    this.auth.resendConfirmation({ email: this.email.value }).subscribe({
      next: (response) => this.resendMessage.set(response.message),
      error: () => this.resendError.set('We could not request a confirmation email. Please try again.')
    });
  }
}

function hasConfirmationSuccess(state: unknown): boolean {
  return typeof state === 'object' && state !== null &&
    (state as { confirmationSuccess?: unknown }).confirmationSuccess === true;
}
