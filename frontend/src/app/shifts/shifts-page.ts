import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { EmployeeForm } from '../employees/employee-form';
import { EmployeesPanel } from '../employees/employees-panel';
import { EmployeesApi } from '../core/employees-api';
import { describeError } from '../core/errors';
import { Employee, ImportReport, Shift, ShiftFilter } from '../core/models';
import { ShiftsApi } from '../core/shifts-api';
import { ImportReportPanel } from './import-report';
import { ShiftForm } from './shift-form';

@Component({
  selector: 'app-shifts-page',
  imports: [ShiftForm, EmployeeForm, EmployeesPanel, ImportReportPanel],
  templateUrl: './shifts-page.html',
})
export class ShiftsPage implements OnInit {
  private readonly shiftsApi = inject(ShiftsApi);
  private readonly employeesApi = inject(EmployeesApi);

  protected readonly shifts = signal<Shift[]>([]);
  protected readonly holidaysAvailable = signal(true);
  protected readonly employees = signal<Employee[]>([]);

  protected readonly formOpen = signal(false);
  /** El turno que se está editando; null = el formulario (si está abierto) crea uno nuevo. */
  protected readonly editing = signal<Shift | null>(null);
  protected readonly employeeFormOpen = signal(false);
  protected readonly employeesPanelOpen = signal(false);
  protected readonly importReport = signal<ImportReport | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(false);
  /** Hay una importación o exportación en curso. */
  protected readonly busy = signal(false);

  /** Filtros del listado. Desde/Hasta y el colaborador también delimitan la exportación. */
  protected readonly filter = signal<ShiftFilter>({ employeeId: null, from: '', to: '', holiday: null });
  protected readonly filtered = computed(() => {
    const f = this.filter();
    return f.employeeId !== null || f.from !== '' || f.to !== '' || f.holiday !== null;
  });
  protected readonly invalidPeriod = computed(() => {
    const { from, to } = this.filter();
    return from !== '' && to !== '' && from > to;
  });
  protected readonly canExport = computed(
    () => this.filter().from !== '' && this.filter().to !== '' && !this.invalidPeriod() && !this.busy(),
  );

  /** Descarta respuestas de listados anteriores cuando los filtros cambian rápido. */
  private listRequest = 0;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    try {
      this.employees.set(await this.employeesApi.list());
    } catch (error) {
      this.error.set(describeError(error));
    }
    await this.loadShifts();
  }

  private async loadShifts(): Promise<void> {
    if (this.invalidPeriod()) {
      return; // el aviso se muestra junto a los filtros; no tiene sentido pedir un 400
    }

    const request = ++this.listRequest;
    this.loading.set(true);
    try {
      const list = await this.shiftsApi.list(this.filter());
      if (request === this.listRequest) {
        this.shifts.set(list.items);
        this.holidaysAvailable.set(list.holidaysAvailable);
      }
    } catch (error) {
      if (request === this.listRequest) {
        this.error.set(describeError(error));
      }
    } finally {
      if (request === this.listRequest) {
        this.loading.set(false);
      }
    }
  }

  protected setEmployeeFilter(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.applyFilter({ employeeId: value === '' ? null : Number(value) });
  }

  protected setFrom(event: Event): void {
    this.applyFilter({ from: (event.target as HTMLInputElement).value });
  }

  protected setTo(event: Event): void {
    this.applyFilter({ to: (event.target as HTMLInputElement).value });
  }

  protected setHolidayFilter(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.applyFilter({ holiday: value === '' ? null : value === 'true' });
  }

  protected clearFilters(): void {
    this.filter.set({ employeeId: null, from: '', to: '', holiday: null });
    void this.loadShifts();
  }

  private applyFilter(change: Partial<ShiftFilter>): void {
    this.filter.update((current) => ({ ...current, ...change }));
    void this.loadShifts();
  }

  protected newShift(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  protected edit(shift: Shift): void {
    this.editing.set(shift);
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
    this.editing.set(null);
  }

  protected async onSaved(): Promise<void> {
    this.closeForm();
    this.error.set(null);
    await this.load();
  }

  protected async onEmployeeSaved(): Promise<void> {
    this.employeeFormOpen.set(false);
    this.error.set(null);
    await this.load(); // el nuevo colaborador aparece ya en el selector del formulario de turnos
  }

  protected async onEmployeeDeleted(id: number): Promise<void> {
    this.error.set(null);
    // Sus turnos ya no existen: no se puede seguir filtrando por él ni editando uno de ellos.
    if (this.filter().employeeId === id) {
      this.filter.update((current) => ({ ...current, employeeId: null }));
    }
    if (this.editing()?.employeeId === id) {
      this.closeForm();
    }
    await this.load();
  }

  protected async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // permite volver a elegir el mismo archivo más adelante
    if (!file) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.importReport.set(null);
    try {
      this.importReport.set(await this.shiftsApi.import(file));
      await this.load();
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async export(): Promise<void> {
    if (!this.canExport()) {
      return;
    }

    const { from, to, employeeId } = this.filter();
    this.busy.set(true);
    this.error.set(null);
    try {
      const { blob, fileName } = await this.shiftsApi.export(from, to, employeeId);
      this.download(blob, fileName);
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.busy.set(false);
    }
  }

  private download(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
