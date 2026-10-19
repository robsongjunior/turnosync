import { Component, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CollaboratorsApi } from '../core/collaborators-api';
import { describeError, fieldErrorsOf } from '../core/errors';
import { CollaboratorRequest } from '../core/models';

@Component({
  selector: 'app-collaborator-form',
  imports: [ReactiveFormsModule],
  templateUrl: './collaborator-form.html',
})
export class CollaboratorForm {
  private readonly api = inject(CollaboratorsApi);

  readonly saved = output<void>();
  readonly cancelled = output<void>();

  /** Solo forma básica; que el país exista y que el email no esté repetido lo valida la API. */
  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    countryCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
  });

  protected readonly submitting = signal(false);
  /** Errores por campo devueltos por la API (claves en camelCase). */
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  /** Error del formulario completo, como el 409 por email repetido. */
  protected readonly formError = signal<string | null>(null);

  protected showError(name: 'name' | 'email' | 'countryCode'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  protected serverErrorsOf(name: string): string[] {
    return this.serverErrors()[name] ?? [];
  }

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    this.submitting.set(true);
    this.serverErrors.set({});
    this.formError.set(null);

    const { name, email, countryCode } = this.form.getRawValue();
    const body: CollaboratorRequest = { name: name.trim(), email: email.trim(), countryCode: countryCode.toUpperCase() };

    try {
      await this.api.create(body);
      this.saved.emit();
    } catch (error) {
      const fieldErrors = fieldErrorsOf(error);
      this.serverErrors.set(fieldErrors);
      // Sin errores por campo (409 por email repetido, red): se muestra un mensaje arriba del formulario.
      this.formError.set(Object.keys(fieldErrors).length > 0 ? null : describeError(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
