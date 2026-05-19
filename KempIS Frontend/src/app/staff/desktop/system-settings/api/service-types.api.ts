import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { ServiceType, ServiceTypeRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class ServiceTypesApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly ServiceType[]> {
    return this.api.get<readonly ServiceType[]>("/service-types");
  }

  create(body: ServiceTypeRequest): Observable<string> {
    return this.api.post<string>("/service-types", body);
  }

  update(id: string, body: ServiceTypeRequest): Observable<void> {
    return this.api.put<void>(`/service-types/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/service-types/${id}`);
  }
}
