import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { Language } from "../../staff/desktop/system-settings/shared/types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class LanguagesStore {
  private readonly apiClient = inject(ApiClient);

  readonly languages = httpResource<readonly Language[]>(() =>
    this.apiClient.url("/languages")
  );

  readonly all = computed<readonly Language[]>(() =>
    this.languages.hasValue() ? this.languages.value() : []
  );

  readonly byId = computed<ReadonlyMap<string, Language>>(
    () => new Map(this.all().map(l => [l.id, l]))
  );

  reload(): void {
    this.languages.reload();
  }
}
