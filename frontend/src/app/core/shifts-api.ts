import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ImportReport, Shift, ShiftFilter, ShiftList, ShiftRequest } from './models';

export interface DownloadedFile {
  fileName: string;
  blob: Blob;
}

@Injectable({ providedIn: 'root' })
export class ShiftsApi {
  private readonly http = inject(HttpClient);

  list(filter: ShiftFilter): Promise<ShiftList> {
    return firstValueFrom(this.http.get<ShiftList>('/api/shifts', { params: toParams(filter) }));
  }

  create(body: ShiftRequest): Promise<Shift> {
    return firstValueFrom(this.http.post<Shift>('/api/shifts', body));
  }

  update(id: number, body: ShiftRequest): Promise<Shift> {
    return firstValueFrom(this.http.put<Shift>(`/api/shifts/${id}`, body));
  }

  import(file: File): Promise<ImportReport> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(this.http.post<ImportReport>('/api/shifts/import', form));
  }

  /** El filtro de festivos no se aplica a la exportación: generar el archivo no depende de la API externa. */
  async export(from: string, to: string, collaboratorId: number | null): Promise<DownloadedFile> {
    try {
      const response = await firstValueFrom(
        this.http.get('/api/shifts/export', {
          params: toParams({ from, to, collaboratorId, holiday: null }),
          observe: 'response',
          responseType: 'blob',
        }),
      );

      // El nombre lo da el servidor (turnos_{from}_{to}.xlsx); el valor de respaldo cubre un proxy que elimine la cabecera.
      const disposition = response.headers.get('Content-Disposition') ?? '';
      const fileName =
        /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? `turnos_${from}_${to}.xlsx`;

      return { fileName, blob: response.body as Blob };
    } catch (error) {
      throw await withJsonBody(error);
    }
  }
}

/** Solo envía los filtros con valor. */
function toParams(filter: ShiftFilter): HttpParams {
  let params = new HttpParams();
  if (filter.collaboratorId !== null) {
    params = params.set('collaboratorId', filter.collaboratorId);
  }
  if (filter.from) {
    params = params.set('from', filter.from);
  }
  if (filter.to) {
    params = params.set('to', filter.to);
  }
  if (filter.holiday !== null) {
    params = params.set('holiday', filter.holiday);
  }
  return params;
}

/** Con responseType 'blob' el cuerpo de un error también llega como Blob; lo devuelve ya convertido a JSON. */
async function withJsonBody(error: unknown): Promise<unknown> {
  if (!(error instanceof HttpErrorResponse) || !(error.error instanceof Blob)) {
    return error;
  }

  try {
    const parsed: unknown = JSON.parse(await error.error.text());
    return new HttpErrorResponse({
      error: parsed,
      headers: error.headers,
      status: error.status,
      statusText: error.statusText,
      url: error.url ?? undefined,
    });
  } catch {
    return error;
  }
}
