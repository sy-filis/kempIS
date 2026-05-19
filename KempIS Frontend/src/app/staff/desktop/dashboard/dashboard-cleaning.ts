import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";

import { ApiClient } from "../../../core/api/api-client";
import { SpotsStore } from "../../../core/spots/spots.store";
import { dateToIso } from "../../../shared/date-iso";
import type { CleaningPlanDetail } from "../../api/cleaning.types";

type CleaningTile = {
  readonly id: string;
  readonly done: boolean;
};

type CleaningRush = {
  readonly id: string;
  readonly priority: "high" | "normal";
  readonly note: string | null;
};

type CleaningView = {
  readonly done: number;
  readonly total: number;
  readonly tiles: readonly CleaningTile[];
  readonly rush: readonly CleaningRush[];
};

const EMPTY: CleaningView = { done: 0, total: 0, tiles: [], rush: [] };

@Component({
  selector: "kemp-is-dash-cleaning",
  imports: [],
  templateUrl: "./dashboard-cleaning.html",
  styleUrl: "./dashboard-cleaning.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashCleaningPanel {
  private readonly apiClient = inject(ApiClient);
  private readonly spotsStore = inject(SpotsStore);

  private readonly today = dateToIso(new Date());

  private readonly planDetail = httpResource<CleaningPlanDetail>(() =>
    this.apiClient.url(`/cleaning-plans/${this.today}`)
  );

  protected readonly cleaning = computed<CleaningView>(() => {
    if (!this.planDetail.hasValue()) {
      return EMPTY;
    }
    const infos = this.planDetail.value().cleanInfos;
    const total = infos.length;
    const done = infos.filter(
      ci => ci.completedAtUtc !== null && ci.completedAtUtc !== 0
    ).length;
    const tiles: readonly CleaningTile[] = infos.map(ci => ({
      id: this.spotsStore.nameOf(ci.spotId),
      done: ci.completedAtUtc !== null && ci.completedAtUtc !== 0,
    }));
    const rush: readonly CleaningRush[] = infos
      .filter(ci => ci.completedAtUtc === null || ci.completedAtUtc === 0)
      .map(ci => ({
        id: this.spotsStore.nameOf(ci.spotId),
        priority: "normal" as const,
        note: ci.note,
      }));
    return { done, total, tiles, rush };
  });

  protected readonly cleaningPct = computed(() => {
    const c = this.cleaning();
    return c.total === 0 ? 0 : Math.round((c.done / c.total) * 100);
  });
}
