import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CollaboratorsApi } from '../core/collaborators-api';
import { describeError } from '../core/errors';
import { Collaborator, ImportReport, Shift } from '../core/models';
import { ShiftsApi } from '../core/shifts-api';
import { ImportReportPanel } from './import-report';
import { ShiftForm } from './shift-form';

@Component({
  selector: 'app-shifts-page',
  imports: [ShiftForm, ImportReportPanel],
  templateUrl: './shifts-page.html',
})
export class ShiftsPage implements OnInit {
  private readonly shiftsApi = inject(ShiftsApi);
  private readonly collaboratorsApi = inject(CollaboratorsApi);

  protected readonly shifts = signal<Shift[]>([]);
  protected readonly holidaysAvailable = signal(true);
  protected readonly collaborators = signal<Collaborator[]>([]);

  protected readonly formOpen = signal(false);
  /** Turno em edição; null = o formulário (se aberto) cria um novo. */
  protected readonly editing = signal<Shift | null>(null);
  protected readonly importReport = signal<ImportReport | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(false);
  /** Importação ou exportação em andamento. */
  protected readonly busy = signal(false);

  protected readonly exportFrom = signal('');
  protected readonly exportTo = signal('');
  protected readonly canExport = computed(() => this.exportFrom() !== '' && this.exportTo() !== '' && !this.busy());

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [list, collaborators] = await Promise.all([this.shiftsApi.list(), this.collaboratorsApi.list()]);
      this.shifts.set(list.items);
      this.holidaysAvailable.set(list.holidaysAvailable);
      this.collaborators.set(collaborators);
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.loading.set(false);
    }
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

  protected async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // permite escolher o mesmo arquivo de novo depois
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

  protected setExportFrom(event: Event): void {
    this.exportFrom.set((event.target as HTMLInputElement).value);
  }

  protected setExportTo(event: Event): void {
    this.exportTo.set((event.target as HTMLInputElement).value);
  }

  protected async export(): Promise<void> {
    const from = this.exportFrom();
    const to = this.exportTo();
    if (!this.canExport()) {
      return;
    }

    if (from > to) {
      this.error.set("'From' must be on or before 'To'.");
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const { blob, fileName } = await this.shiftsApi.export(from, to);
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
