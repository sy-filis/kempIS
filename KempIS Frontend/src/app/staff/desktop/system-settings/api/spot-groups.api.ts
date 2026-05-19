import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { CatalogueSpotGroup, SpotGroupRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class SpotGroupsApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly CatalogueSpotGroup[]> {
    return this.api.get<readonly CatalogueSpotGroup[]>("/spot-groups");
  }

  create(body: SpotGroupRequest): Observable<string> {
    return this.api.post<string>("/spot-groups", body);
  }

  update(id: string, body: SpotGroupRequest): Observable<void> {
    return this.api.put<void>(`/spot-groups/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/spot-groups/${id}`);
  }
}
