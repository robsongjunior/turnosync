import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ImportReport, Shift, ShiftList, ShiftRequest } from './models';

export interface DownloadedFile {
  fileName: string;
  blob: Blob;
}

@Injectable({ providedIn: 'root' })
export class ShiftsApi {
  private readonly http = inject(HttpClient);

  list(): Promise<ShiftList> {
    return firstValueFrom(this.http.get<ShiftList>('/api/shifts'));
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

  async export(from: string, to: string): Promise<DownloadedFile> {
    try {
      const response = await firstValueFrom(
        this.http.get('/api/shifts/export', {
          params: { from, to },
          observe: 'response',
          responseType: 'blob',
        }),
      );

      // The name comes from the server (shifts_{from}_{to}.xlsx); the fallback covers a proxy that strips the header.
      const disposition = response.headers.get('Content-Disposition') ?? '';
      const fileName =
        /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? `shifts_${from}_${to}.xlsx`;

      return { fileName, blob: response.body as Blob };
    } catch (error) {
      throw await withJsonBody(error);
    }
  }
}

/** With responseType 'blob' an error body also arrives as a Blob; returns it already parsed as JSON. */
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
