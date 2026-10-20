import { Component, inject, input, output, signal } from '@angular/core';
import { EmployeesApi } from '../core/employees-api';
import { describeError } from '../core/errors';
import { Employee } from '../core/models';

@Component({
  selector: 'app-employees-panel',
  templateUrl: './employees-panel.html',
})
export class EmployeesPanel {
  private readonly api = inject(EmployeesApi);

  readonly employees = input.required<Employee[]>();
  /** Emite el id del colaborador borrado para que la página recargue colaboradores y turnos. */
  readonly deleted = output<number>();
  readonly closed = output<void>();

  /** Id del colaborador que se está borrando; bloquea los botones mientras tanto. */
  protected readonly deleting = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);

  protected async remove(employee: Employee): Promise<void> {
    // El borrado arrastra los turnos del colaborador, así que se pide confirmación.
    if (!confirm(`¿Eliminar a ${employee.name}? También se eliminarán todos sus turnos.`)) {
      return;
    }

    this.deleting.set(employee.id);
    this.error.set(null);
    try {
      await this.api.delete(employee.id);
      this.deleted.emit(employee.id);
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.deleting.set(null);
    }
  }
}
