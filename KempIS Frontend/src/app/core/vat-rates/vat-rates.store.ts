import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { VatRate } from "../../staff/desktop/system-settings/shared/types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class VatRatesStore {
  private readonly apiClient = inject(ApiClient);

  readonly vatRates = httpResource<readonly VatRate[]>(() =>
    this.apiClient.url("/vat-rates")
  );

  readonly all = computed<readonly VatRate[]>(() =>
    this.vatRates.hasValue() ? this.vatRates.value() : []
  );

  /** id → numeric percentage (e.g. `21`, not `0.21`). */
  readonly rateById = computed<ReadonlyMap<string, number>>(
    () => new Map(this.all().map(r => [r.id, r.rate]))
  );

  reload(): void {
    this.vatRates.reload();
  }
}
