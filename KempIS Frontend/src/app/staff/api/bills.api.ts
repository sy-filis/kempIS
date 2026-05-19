import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  BillDetail,
  CreateBillRequest,
  CreateBillResponse,
  CreateRepairBillRequest,
  CreateRepairBillResponse,
} from "./bills.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class BillsApi {
  private readonly api = inject(ApiClient);
  private readonly http = inject(HttpClient);

  create(request: CreateBillRequest): Observable<CreateBillResponse> {
    return this.api.post<CreateBillResponse>("/bills", request);
  }

  /** Each repair line must match an original by
   *  `(serviceId, unitPrice, vatRatePercentage)`; quantity is capped at
   *  the original minus prior repairs (see
   *  `CreateRepairBillCommandHandler.cs`). */
  createRepair(
    request: CreateRepairBillRequest
  ): Observable<CreateRepairBillResponse> {
    return this.api.post<CreateRepairBillResponse>("/bills/repairs", request);
  }

  getById(id: string): Observable<BillDetail> {
    return this.api.get<BillDetail>(`/bills/${encodeURIComponent(id)}`);
  }

  getPdf(id: string): Observable<Blob> {
    return this.http.get(this.api.url(`/bills/${encodeURIComponent(id)}/pdf`), {
      responseType: "blob",
    });
  }

  /** QR registration sticker PDF (62x19 mm). */
  getSticker(id: string): Observable<Blob> {
    return this.http.get(
      this.api.url(`/bills/${encodeURIComponent(id)}/sticker.pdf`),
      { responseType: "blob" }
    );
  }
}
