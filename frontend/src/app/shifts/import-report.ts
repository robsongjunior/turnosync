import { Component, input, output } from '@angular/core';
import { ImportReport } from '../core/models';

@Component({
  selector: 'app-import-report',
  templateUrl: './import-report.html',
})
export class ImportReportPanel {
  readonly report = input.required<ImportReport>();
  readonly closed = output<void>();
}
