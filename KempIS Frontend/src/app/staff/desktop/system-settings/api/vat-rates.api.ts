import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { VatRate, VatRateRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class VatRatesApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly VatRate[]> {
    return this.api.get<readonly VatRate[]>("/vat-rates");
  }

  create(body: VatRateRequest): Observable<string> {
    return this.api.post<string>("/vat-rates", body);
  }

  update(id: string, body: VatRateRequest): Observable<void> {
    return this.api.put<void>(`/vat-rates/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/vat-rates/${id}`);
  }
}
