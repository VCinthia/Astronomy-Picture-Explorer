import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="mx-auto max-w-md" aria-labelledby="forgot-password-title">
      <p class="text-caption font-medium uppercase tracking-widest text-accent">Your account</p>
      <h1 id="forgot-password-title" class="mt-2 text-title font-bold text-content-primary">Reset password</h1>
      <p class="mt-3 text-body text-content-secondary">
        Enter your email and we will send a reset link if the account is eligible.
      </p>

      @if (successMessage()) {
        <p role="status" aria-live="polite" class="mt-6 rounded-button border border-accent/50 bg-accent/10 px-4 py-3 text-meta text-content-primary">
          {{ successMessage() }} Check your inbox for a reset link.
        </p>
      } @else {
        <form class="mt-8 space-y-5" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div>
            <label for="forgot-password-email" class="block text-meta font-medium text-content-primary">Email</label>
            <input
              id="forgot-password-email"
              type="email"
              autocomplete="email"
              maxlength="256"
              formControlName="email"
              [attr.aria-invalid]="email.invalid && (email.touched || submitted())"
              aria-describedby="forgot-password-email-error"
              class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            @if (email.invalid && (email.touched || submitted())) {
              <p id="forgot-password-email-error" class="mt-2 text-meta text-red-300">Enter a valid email address.</p>
            }
          </div>

          @if (errorMessage()) {
            <p role="alert" class="rounded-button border border-red-400/60 bg-red-400/10 px-4 py-3 text-meta text-red-100">
              {{ errorMessage() }}
            </p>
          }

          <button
            type="submit"
            [disabled]="auth.loading()"
            class="inline-flex w-full justify-center rounded-button bg-accent px-5 py-3 text-meta font-semibold text-space-base transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            {{ auth.loading() ? 'Sending reset link…' : 'Send reset link' }}
          </button>
        </form>
      }

      <p class="mt-6 text-meta text-content-secondary">
        Remembered your password?
        <a routerLink="/login" class="font-medium text-accent underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Sign in</a>.
      </p>
    </section>
  `
})
export class ForgotPasswordComponent {
  readonly auth = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  readonly submitted = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]]
  });

  get email() {
    return this.form.controls.email;
  }

  submit(): void {
    this.submitted.set(true);
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      next: (response) => this.successMessage.set(response.message),
      error: () => this.errorMessage.set('We could not request a password reset. Please try again.')
    });
  }
}
