import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Collaborator, CollaboratorRequest } from './models';

@Injectable({ providedIn: 'root' })
export class CollaboratorsApi {
  private readonly http = inject(HttpClient);

  /** Alimenta el selector de colaboradores del formulario de turnos. */
  list(): Promise<Collaborator[]> {
    return firstValueFrom(this.http.get<Collaborator[]>('/api/collaborators'));
  }

  create(body: CollaboratorRequest): Promise<Collaborator> {
    return firstValueFrom(this.http.post<Collaborator>('/api/collaborators', body));
  }
}
