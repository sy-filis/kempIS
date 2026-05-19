import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { Nationality } from "../../staff/desktop/system-settings/shared/types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class NationalitiesStore {
  private readonly apiClient = inject(ApiClient);

  readonly nationalities = httpResource<readonly Nationality[]>(() =>
    this.apiClient.url("/nationalities")
  );

  readonly all = computed<readonly Nationality[]>(() =>
    this.nationalities.hasValue() ? this.nationalities.value() : []
  );

  readonly byId = computed<ReadonlyMap<string, Nationality>>(
    () => new Map(this.all().map(n => [n.id, n]))
  );

  alpha3Of(id: string): string {
    return this.byId().get(id)?.alpha3 ?? "-";
  }

  reload(): void {
    this.nationalities.reload();
  }
}
