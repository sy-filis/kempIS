import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class ReservationSpotItemsApi {
  private readonly api = inject(ApiClient);

  /** Idempotent on the backend; the parent reservation must be
   *  Confirmed or CheckedIn. */
  giveKey(id: string): Observable<void> {
    return this.api.post<void>(`/reservation-spot-items/${id}/give-key`, null);
  }
}
