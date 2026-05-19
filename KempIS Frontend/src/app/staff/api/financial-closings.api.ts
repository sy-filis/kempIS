import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  CreateFinancialClosingResponse,
  FinancialClosingDetail,
} from "./financial-closings.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class FinancialClosingsApi {
  private readonly api = inject(ApiClient);
  private readonly http = inject(HttpClient);

  /** 409 if there are no eligible open bills. */
  create(): Observable<CreateFinancialClosingResponse> {
    return this.api.post<CreateFinancialClosingResponse>(
      "/financial-closings",
      null
    );
  }

  getById(id: string): Observable<FinancialClosingDetail> {
    return this.api.get<FinancialClosingDetail>(`/financial-closings/${id}`);
  }

  getPdf(id: string): Observable<Blob> {
    return this.http.get(this.api.url(`/financial-closings/${id}/pdf`), {
      responseType: "blob",
    });
  }
}
