import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, Validators, NonNullableFormBuilder } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="mx-auto max-w-md" aria-labelledby="register-title">
      <p class="text-caption font-medium uppercase tracking-widest text-accent">Your account</p>
      <h1 id="register-title" class="mt-2 text-title font-bold text-content-primary">Create account</h1>
      <p class="mt-3 text-body text-content-secondary">
        Save your favorite astronomy pictures after confirming your email.
      </p>

      <form class="mt-8 space-y-5" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div>
          <label for="register-email" class="block text-meta font-medium text-content-primary">Email</label>
          <input
            id="register-email"
            type="email"
            autocomplete="email"
            maxlength="256"
            formControlName="email"
            [attr.aria-invalid]="email.invalid && (email.touched || submitted())"
            aria-describedby="register-email-error"
            class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          @if (email.invalid && (email.touched || submitted())) {
            <p id="register-email-error" class="mt-2 text-meta text-red-300">Enter a valid email address.</p>
          }
        </div>

        <div>
          <label for="register-password" class="block text-meta font-medium text-content-primary">Password</label>
          <input
            id="register-password"
            type="password"
            autocomplete="new-password"
            formControlName="password"
            [attr.aria-invalid]="password.invalid && (password.touched || submitted())"
            aria-describedby="register-password-help register-password-error"
            class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          <p id="register-password-help" class="mt-2 text-meta text-content-secondary">
            Use at least 6 characters. Your account may require additional password checks.
          </p>
          @if (password.invalid && (password.touched || submitted())) {
            <p id="register-password-error" class="mt-2 text-meta text-red-300">Enter a password with at least 6 characters.</p>
          }
        </div>

        @if (successMessage()) {
          <p role="status" aria-live="polite" class="rounded-button border border-accent/50 bg-accent/10 px-4 py-3 text-meta text-content-primary">
            {{ successMessage() }} Check your inbox for a confirmation link.
          </p>
        }
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
          {{ auth.loading() ? 'Creating account…' : 'Create account' }}
        </button>
      </form>

      <p class="mt-6 text-meta text-content-secondary">
        Already confirmed?
        <a routerLink="/login" class="font-medium text-accent underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Sign in</a>.
      </p>
    </section>
  `
})
export class RegisterComponent {
  readonly auth = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  readonly submitted = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  get email() {
    return this.form.controls.email;
  }

  get password() {
    return this.form.controls.password;
  }

  submit(): void {
    this.submitted.set(true);
    this.successMessage.set(null);
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.auth.register(this.form.getRawValue()).subscribe({
      next: (response) => this.successMessage.set(response.message),
      error: (problem) => this.errorMessage.set(
        firstValidationMessage(problem.errors) ??
        'We could not start your registration. Please try again.'
      )
    });
  }
}

function firstValidationMessage(
  errors: Readonly<Record<string, readonly string[]>> | null
): string | null {
  if (errors === null) {
    return null;
  }

  return Object.values(errors).flat().find((message) => message.length > 0) ?? null;
}
