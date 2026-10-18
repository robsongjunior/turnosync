import { Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Collaborator, Shift, ShiftRequest } from '../core/models';
import { describeError, fieldErrorsOf } from '../core/errors';
import { ShiftsApi } from '../core/shifts-api';

/** An equal start and end is not a shift. The rest (overlap, collaborator) is validated by the API. */
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

  /** The shift being edited; null to create a new one. */
  readonly shift = input<Shift | null>(null);
  readonly collaborators = input.required<Collaborator[]>();

  readonly saved = output<void>();
  readonly cancelled = output<void>();

  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      collaboratorId: [0, [Validators.required, Validators.min(1)]],
      date: ['', Validators.required],
      startTime: ['', Validators.required],
      endTime: ['', Validators.required],
    },
    { validators: startDiffersFromEnd },
  );

  protected readonly submitting = signal(false);
  /** Per-field errors returned by the API (camelCase keys). */
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  /** Form-level error, such as the 409 overlap. */
  protected readonly formError = signal<string | null>(null);

  constructor() {
    // Fills the form when the shift being edited changes (or clears it, when creating).
    effect(() => {
      const shift = this.shift();
      untracked(() => {
        this.form.reset({
          collaboratorId: shift?.collaboratorId ?? 0,
          date: shift?.date ?? '',
          startTime: shift?.startTime ?? '',
          endTime: shift?.endTime ?? '',
        });
        this.serverErrors.set({});
        this.formError.set(null);
      });
    });
  }

  protected showError(name: 'collaboratorId' | 'date' | 'startTime' | 'endTime'): boolean {
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
      // No per-field errors (404, 409 overlap, network): show a message at the top of the form.
      this.formError.set(Object.keys(fieldErrors).length > 0 ? null : describeError(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
