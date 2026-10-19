export interface Collaborator {
  id: number;
  name: string;
  email: string;
  countryCode: string;
}

export interface CollaboratorRequest {
  name: string;
  email: string;
  /** Código ISO 3166-1 alfa-2, por ejemplo ES. */
  countryCode: string;
}

export interface Shift {
  id: number;
  collaboratorId: number;
  collaboratorName: string;
  /** yyyy-MM-dd */
  date: string;
  /** HH:mm */
  startTime: string;
  /** HH:mm. Anterior a startTime cuando el turno cruza la medianoche. */
  endTime: string;
  /** null = desconocido (la API de festivos falló para el país del colaborador). */
  isHoliday: boolean | null;
}

export interface ShiftList {
  items: Shift[];
  holidaysAvailable: boolean;
}

export interface ShiftRequest {
  collaboratorId: number;
  date: string;
  startTime: string;
  endTime: string;
}

export interface ImportedRow {
  row: number;
  email: string;
}

export interface FailedRow {
  row: number;
  email: string | null;
  reasons: string[];
}

export interface ImportReport {
  imported: ImportedRow[];
  failed: FailedRow[];
}

/** ASP.NET Core ProblemDetails / ValidationProblemDetails. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
