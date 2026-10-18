import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Collaborator } from './models';

@Injectable({ providedIn: 'root' })
export class CollaboratorsApi {
  private readonly http = inject(HttpClient);

  /** Alimenta o select do formulário de turno. */
  list(): Promise<Collaborator[]> {
    return firstValueFrom(this.http.get<Collaborator[]>('/api/collaborators'));
  }
}
