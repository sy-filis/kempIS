import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { Spot } from "../../staff/api/spots.types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class SpotsStore {
  private readonly apiClient = inject(ApiClient);

  readonly spots = httpResource<readonly Spot[]>(() =>
    this.apiClient.url("/spots")
  );

  readonly nameById = computed<ReadonlyMap<string, string>>(() => {
    if (!this.spots.hasValue()) {
      return new Map();
    }
    return new Map(this.spots.value().map(s => [s.id, s.name]));
  });

  nameOf(id: string): string {
    return this.nameById().get(id) ?? "-";
  }

  reload(): void {
    this.spots.reload();
  }
}
