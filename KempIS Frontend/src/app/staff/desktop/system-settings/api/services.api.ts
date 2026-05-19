import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { Service, ServiceRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class ServicesApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly Service[]> {
    return this.api.get<readonly Service[]>("/services");
  }

  create(body: ServiceRequest): Observable<string> {
    return this.api.post<string>("/services", body);
  }

  update(id: string, body: ServiceRequest): Observable<void> {
    return this.api.put<void>(`/services/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/services/${id}`);
  }
}
