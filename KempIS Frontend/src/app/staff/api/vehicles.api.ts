import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  VehicleLookupRequest,
  VehicleLookupResponse,
  VehicleRequest,
} from "./vehicles.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class VehiclesApi {
  private readonly api = inject(ApiClient);

  /** Look up a licence plate against active reservations. On a positive
   *  hit the response carries the planned check-out date. */
  lookup(request: VehicleLookupRequest): Observable<VehicleLookupResponse> {
    return this.api.post<VehicleLookupResponse>("/vehicles/lookup", request);
  }

  update(id: string, request: VehicleRequest): Observable<void> {
    return this.api.put<void>(`/vehicles/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/vehicles/${id}`);
  }
}
