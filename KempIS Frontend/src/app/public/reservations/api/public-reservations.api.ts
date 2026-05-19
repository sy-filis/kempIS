import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  AvailabilityResponse,
  CreateWebReservationRequest,
  CreateWebReservationResponse,
  Nationality,
} from "./public-reservations.types";
import { ApiClient } from "../../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class PublicReservationsApi {
  private readonly api = inject(ApiClient);

  getAvailability(from: string, to: string): Observable<AvailabilityResponse> {
    return this.api.get<AvailabilityResponse>("/availability", {
      params: { from, to },
    });
  }

  createWebReservation(
    body: CreateWebReservationRequest
  ): Observable<CreateWebReservationResponse> {
    return this.api.post<CreateWebReservationResponse>(
      "/reservations/web",
      body
    );
  }

  cancelReservationForGuest(id: string, secret: string): Observable<void> {
    return this.api.post<void>(`/reservations/${id}/guest/cancel`, null, {
      params: { secret },
    });
  }

  getNationalities(): Observable<readonly Nationality[]> {
    return this.api.get<readonly Nationality[]>("/nationalities");
  }
}
