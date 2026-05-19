import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { ServiceText, ServiceTextRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class ServiceTextsApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly ServiceText[]> {
    return this.api.get<readonly ServiceText[]>("/service-texts");
  }

  create(body: ServiceTextRequest): Observable<string> {
    return this.api.post<string>("/service-texts", body);
  }

  update(id: string, body: ServiceTextRequest): Observable<void> {
    return this.api.put<void>(`/service-texts/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/service-texts/${id}`);
  }
}
