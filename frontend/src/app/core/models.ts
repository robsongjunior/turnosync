export interface Collaborator {
  id: number;
  name: string;
  email: string;
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
  /** HH:mm. Earlier than startTime when the shift crosses midnight. */
  endTime: string;
  /** null = unknown (the holiday API failed for the collaborator's country). */
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
