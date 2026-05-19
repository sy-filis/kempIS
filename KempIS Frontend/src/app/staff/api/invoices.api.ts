import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  CreateInvoiceRequest,
  CreateInvoiceResponse,
  GetInvoiceResponse,
  LegalEntityFinderResponse,
  MarkInvoiceCreatedRequest,
  MarkInvoicePaidRequest,
  UpdateInvoiceRequest,
} from "./invoices.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class InvoicesApi {
  private readonly api = inject(ApiClient);

  /** Creates a `Draft`. The formal number is assigned later via
   *  `markCreated`. */
  create(request: CreateInvoiceRequest): Observable<CreateInvoiceResponse> {
    return this.api.post<CreateInvoiceResponse>("/invoices", request);
  }

  get(id: string): Observable<GetInvoiceResponse> {
    return this.api.get<GetInvoiceResponse>(
      `/invoices/${encodeURIComponent(id)}`
    );
  }

  /** Backend rejects with 409 once the invoice has been issued or paid. */
  update(id: string, body: UpdateInvoiceRequest): Observable<void> {
    return this.api.put<void>(`/invoices/${encodeURIComponent(id)}`, body);
  }

  /** Promotes `Draft` -> `Created`. 409 if already issued or the
   *  supplied number is taken. */
  markCreated(id: string, body: MarkInvoiceCreatedRequest): Observable<void> {
    return this.api.post<void>(
      `/invoices/${encodeURIComponent(id)}/mark-created`,
      body
    );
  }

  /** Promotes `Created` -> `Paid`. 409 if the invoice is not in
   *  `Created`. */
  markPaid(id: string, body: MarkInvoicePaidRequest): Observable<void> {
    return this.api.post<void>(
      `/invoices/${encodeURIComponent(id)}/mark-paid`,
      body
    );
  }

  /** Czech legal-entity lookup by IČO via the ARES proxy. */
  fromAres(cin: string): Observable<LegalEntityFinderResponse> {
    return this.api.get<LegalEntityFinderResponse>(
      `/legal-entities/ares/${encodeURIComponent(cin)}`
    );
  }
}
