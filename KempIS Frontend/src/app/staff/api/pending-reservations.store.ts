import { httpResource } from "@angular/common/http";
import { computed, effect, inject, Injectable, untracked } from "@angular/core";

import type { Reservation } from "./reservations.types";
import { equal } from "../../../utils/deepEqual";
import { ApiClient } from "../../core/api/api-client";
import { RefreshController } from "../../core/refresh/refresh-controller";

@Injectable({ providedIn: "root" })
export class PendingReservationsStore {
  private readonly apiClient = inject(ApiClient);
  private readonly refresh = inject(RefreshController);

  readonly resource = httpResource<readonly Reservation[]>(
    () => `${this.apiClient.url("/reservations")}?status=Created`,
    { equal }
  );

  readonly value = computed<readonly Reservation[]>(() =>
    this.resource.hasValue() ? this.resource.value() : []
  );

  readonly count = computed<number>(() => this.value().length);

  constructor() {
    effect(() => {
      const t = this.refresh.tick();
      if (t === 0) {
        return;
      }
      untracked(() => {
        this.resource.reload();
      });
    });
  }

  reload(): void {
    this.resource.reload();
  }
}
