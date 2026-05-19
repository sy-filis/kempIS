import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../api/api-client";

export type AddressSuggestion = {
  readonly countryCode: string;
  readonly city: string;
  readonly zipCode: string;
  readonly street: string;
  readonly houseNumber: string;
};

@Injectable({ providedIn: "root" })
export class AddressesApi {
  private readonly api = inject(ApiClient);

  /** Pass `foreign=true` to search outside the host country (when the
   *  guest's country of residence isn't CZ). */
  whisperer(
    query: string,
    foreign: boolean
  ): Observable<readonly AddressSuggestion[]> {
    return this.api.get<readonly AddressSuggestion[]>("/addresses/whisperer", {
      params: { query, foreign: String(foreign) },
    });
  }
}
