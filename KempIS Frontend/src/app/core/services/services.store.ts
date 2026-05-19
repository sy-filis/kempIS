import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { ServiceGroup } from "../../staff/desktop/system-settings/shared/service-groups";
import type { Service } from "../../staff/desktop/system-settings/shared/types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class ServicesStore {
  private readonly apiClient = inject(ApiClient);

  readonly services = httpResource<readonly Service[]>(() =>
    this.apiClient.url("/services")
  );

  readonly active = computed<readonly Service[]>(() =>
    this.services.hasValue()
      ? this.services.value().filter(s => s.isActive)
      : []
  );

  byGroup(group: ServiceGroup): readonly Service[] {
    return this.active().filter(s => s.serviceGroup === group);
  }

  /** Lookup across the full catalogue (active or not). Returns `null`
   *  while the list is loading or when the id isn't known. */
  byId(id: string): Service | null {
    if (!this.services.hasValue()) {
      return null;
    }
    return this.services.value().find(s => s.id === id) ?? null;
  }

  reload(): void {
    this.services.reload();
  }
}
