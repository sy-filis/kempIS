import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../api/api-client";

export type PairCodeResponse = {
  readonly pairCode: string;
  /** ISO 8601 UTC instant. */
  readonly expiresAtUtc: string;
};

@Injectable({ providedIn: "root" })
export class ReceptionPairCodesApi {
  private readonly api = inject(ApiClient);

  /** Mints a single-use pair code valid for ~120 s. */
  create(): Observable<PairCodeResponse> {
    return this.api.post<PairCodeResponse>("/reception/pair-codes", {});
  }
}
