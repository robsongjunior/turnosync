import { Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Employee, Shift, ShiftRequest } from '../core/models';
import { describeError, fieldErrorsOf } from '../core/errors';
import { ShiftsApi } from '../core/shifts-api';

/** Un inicio igual al fin no es un turno. El resto (solapamiento, colaborador) lo valida la API. */
function startDiffersFromEnd(group: AbstractControl): ValidationErrors | null {
  const { startTime, endTime } = group.value as { startTime: string; endTime: string };
  return startTime && endTime && startTime === endTime ? { sameTime: true } : null;
}

@Component({
  selector: 'app-shift-form',
  imports: [ReactiveFormsModule],
  templateUrl: './shift-form.html',
})
export class ShiftForm {
  private readonly api = inject(ShiftsApi);

  /** El turno que se está editando; null para crear uno nuevo. */
  readonly shift = input<Shift | null>(null);
  readonly employees = input.required<Employee[]>();

  readonly saved = output<void>();
  readonly cancelled = output<void>();

  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      employeeId: [0, [Validators.required, Validators.min(1)]],
      date: ['', Validators.required],
      startTime: ['', Validators.required],
      endTime: ['', Validators.required],
    },
    { validators: startDiffersFromEnd },
  );

  protected readonly submitting = signal(false);
  /** Errores por campo devueltos por la API (claves en camelCase). */
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  /** Error del formulario completo, como el 409 por solapamiento. */
  protected readonly formError = signal<string | null>(null);

  constructor() {
    // Rellena el formulario cuando cambia el turno que se edita (o lo vacía, al crear).
    effect(() => {
      const shift = this.shift();
      untracked(() => {
        this.form.reset({
          employeeId: shift?.employeeId ?? 0,
          date: shift?.date ?? '',
          startTime: shift?.startTime ?? '',
          endTime: shift?.endTime ?? '',
        });
        this.serverErrors.set({});
        this.formError.set(null);
      });
    });
  }

  protected showError(name: 'employeeId' | 'date' | 'startTime' | 'endTime'): boolean {
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

    const body: ShiftRequest = this.form.getRawValue();
    const editing = this.shift();

    try {
      await (editing ? this.api.update(editing.id, body) : this.api.create(body));
      this.saved.emit();
    } catch (error) {
      const fieldErrors = fieldErrorsOf(error);
      this.serverErrors.set(fieldErrors);
      // Sin errores por campo (404, 409 por solapamiento, red): se muestra un mensaje arriba del formulario.
      this.formError.set(Object.keys(fieldErrors).length > 0 ? null : describeError(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
