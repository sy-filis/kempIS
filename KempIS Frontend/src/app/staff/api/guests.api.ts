import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type { GuestDetail } from "./guests.types";
import type {
  GuestDocumentType,
  ReservationAddress,
  ReservationDateRange,
} from "./reservations.types";
import { ApiClient } from "../../core/api/api-client";

/** `reservationId` may be `null` for guests not tied to a reservation
 *  (walk-in bill guests, ad-hoc records). */
export type GuestRequest = {
  reservationId: string | null;
  billId: string | null;
  /** Tri-state: `true` / `false` / `null` (unbilled, not asked yet).
   *  Without it the PUT silently drops the receptionist's RP toggle. */
  paysRecreationFee: boolean | null;
  firstName: string; // <= 255
  lastName: string; // <= 255
  nationalityId: string;
  dateOfBirth: string; // YYYY-MM-DD
  /** Required on create; backend rejects null. */
  documentType: GuestDocumentType;
  documentNumber: string; // <= 50, required
  address: ReservationAddress;
  reasonOfStay: string; // <= 500, required
  stayDateRange: ReservationDateRange;
  visaNumber: string | null; // <= 50
  note: string | null; // <= 1000
  scartation: string | null; // YYYY-MM-DD
  checkInAt: string | null; // ISO-8601 UTC
  checkOutAt: string | null; // ISO-8601 UTC
  /** Stored only when nationality requires one (non-Czech). */
  signaturePngBase64: string | null;
};

@Injectable({ providedIn: "root" })
export class GuestsApi {
  private readonly api = inject(ApiClient);

  getById(id: string): Observable<GuestDetail> {
    return this.api.get<GuestDetail>(`/guests/${id}`);
  }

  create(request: GuestRequest): Observable<string> {
    return this.api.post<string>("/guests", request);
  }

  update(id: string, request: GuestRequest): Observable<void> {
    return this.api.put<void>(`/guests/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.api.delete<void>(`/guests/${id}`);
  }

  setSignature(id: string, signaturePngBase64: string): Observable<void> {
    return this.api.put<void>(`/guests/${id}/signature`, {
      signaturePngBase64,
    });
  }

  clearSignature(id: string): Observable<void> {
    return this.api.delete<void>(`/guests/${id}/signature`);
  }

  /** Submits unreported non-Czech checked-in guests to the Czech Ubyport
   *  register. Returns 204 even when there is nothing to report. */
  reportToPolice(): Observable<void> {
    return this.api.post<void>("/guests/report-to-police", null);
  }
}
