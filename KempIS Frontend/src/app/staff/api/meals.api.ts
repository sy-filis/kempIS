import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type { ReplaceMealRequest } from "./meals.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class MealsApi {
  private readonly api = inject(ApiClient);

  /** Upserts the meal counts for the reservation on the supplied date.
   *  The date must fall within the reservation's stay period. */
  replace(
    reservationId: string,
    request: ReplaceMealRequest
  ): Observable<void> {
    return this.api.post<void>(`/reservations/${reservationId}/meals`, request);
  }

  deleteAll(reservationId: string): Observable<void> {
    return this.api.delete<void>(`/reservations/${reservationId}/meals`);
  }
}
