import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Employee, EmployeeRequest } from './models';

@Injectable({ providedIn: 'root' })
export class EmployeesApi {
  private readonly http = inject(HttpClient);

  /** Alimenta el selector de colaboradores del formulario de turnos. */
  list(): Promise<Employee[]> {
    return firstValueFrom(this.http.get<Employee[]>('/api/employees'));
  }

  create(body: EmployeeRequest): Promise<Employee> {
    return firstValueFrom(this.http.post<Employee>('/api/employees', body));
  }
}
