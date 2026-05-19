import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { CatalogueSpot, SpotRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class SpotsApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly CatalogueSpot[]> {
    return this.api.get<readonly CatalogueSpot[]>("/spots");
  }

  create(body: SpotRequest): Observable<string> {
    return this.api.post<string>("/spots", body);
  }

  update(id: string, body: SpotRequest): Observable<void> {
    return this.api.put<void>(`/spots/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/spots/${id}`);
  }
}
