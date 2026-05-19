import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  TransactionResult,
  TransactionState,
  VirtualServiceCounter,
} from "./edoklady.types";
import { ApiClient } from "../api/api-client";

/** Endpoints are role-gated to Receptionist or Manager on the
 *  backend, so 401/403 surfaces via the standard http-error
 *  interceptor. */
@Injectable({ providedIn: "root" })
export class EdokladyApi {
  private readonly api = inject(ApiClient);

  createCounter(name: string | null = null): Observable<VirtualServiceCounter> {
    return this.api.post<VirtualServiceCounter>(
      "/edoklady/virtual-service-counters",
      { name }
    );
  }

  getCounter(id: string): Observable<VirtualServiceCounter> {
    return this.api.get<VirtualServiceCounter>(
      `/edoklady/virtual-service-counters/${encodeURIComponent(id)}`
    );
  }

  startPresentation(
    virtualServiceCounterId: string
  ): Observable<{ transactionId: string }> {
    return this.api.post<{ transactionId: string }>("/edoklady/presentations", {
      virtualServiceCounterId,
    });
  }

  getTransaction(transactionId: string): Observable<TransactionState> {
    return this.api.get<TransactionState>(
      `/edoklady/presentations/${encodeURIComponent(transactionId)}`
    );
  }

  getTransactionResult(transactionId: string): Observable<TransactionResult> {
    return this.api.get<TransactionResult>(
      `/edoklady/presentations/${encodeURIComponent(transactionId)}/result`
    );
  }
}
