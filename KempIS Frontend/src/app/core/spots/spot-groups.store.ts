import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { SpotGroup } from "../../staff/api/spots.types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class SpotGroupsStore {
  private readonly apiClient = inject(ApiClient);

  readonly spotGroups = httpResource<readonly SpotGroup[]>(() =>
    this.apiClient.url("/spot-groups")
  );

  readonly all = computed<readonly SpotGroup[]>(() =>
    this.spotGroups.hasValue() ? this.spotGroups.value() : []
  );

  readonly byId = computed<ReadonlyMap<string, SpotGroup>>(
    () => new Map(this.all().map(g => [g.id, g]))
  );

  reload(): void {
    this.spotGroups.reload();
  }
}
