import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from "@angular/core";

import { MessageModule } from "primeng/message";
import { TableModule } from "primeng/table";

import { ApiClient } from "../../../../core/api/api-client";
import { isApiError } from "../../../../core/api/api-error";
import { dateToIso } from "../../../../shared/date-iso";
import { formatPercent } from "../../../../shared/format-percent";
import type {
  OccupancyStatsResponse,
  OccupancyStatsRow,
} from "../../../api/stats.types";

function rangeDays(from: Date, to: Date): number {
  return Math.floor((to.getTime() - from.getTime()) / 86_400_000) + 1;
}

@Component({
  selector: "kemp-is-stats-occupancy",
  imports: [MessageModule, TableModule],
  templateUrl: "./occupancy.html",
  styleUrl: "./occupancy.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsOccupancyPanel {
  private readonly apiClient = inject(ApiClient);

  protected readonly formatPercent = formatPercent;

  readonly range = input<Date[]>([]);

  protected readonly rangeTooLarge = computed<boolean>(() => {
    const [from, to] = this.range();
    if (!from || !to) {
      return false;
    }
    return rangeDays(from, to) > 366;
  });

  private readonly query = computed<{ from: string; to: string } | null>(() => {
    const [from, to] = this.range();
    if (!from || !to || this.rangeTooLarge()) {
      return null;
    }
    return { from: dateToIso(from), to: dateToIso(to) };
  });

  protected readonly resource = httpResource<OccupancyStatsResponse>(() => {
    const q = this.query();
    if (!q) {
      return undefined;
    }
    const params = new URLSearchParams({ from: q.from, to: q.to });
    return `${this.apiClient.url("/stats/occupancy")}?${params.toString()}`;
  });

  protected readonly rows = computed<OccupancyStatsRow[]>(() =>
    this.resource.hasValue() ? [...this.resource.value().groups] : []
  );

  protected readonly totals = computed<{
    nightsInRange: number;
    totalOccupiedSpotNights: number;
    totalCapacitySpotNights: number;
    totalOccupancyPercent: number;
  } | null>(() => {
    if (!this.resource.hasValue()) {
      return null;
    }
    const v = this.resource.value();
    return {
      nightsInRange: v.nightsInRange,
      totalOccupiedSpotNights: v.totalOccupiedSpotNights,
      totalCapacitySpotNights: v.totalCapacitySpotNights,
      totalOccupancyPercent: v.totalOccupancyPercent,
    };
  });

  protected readonly errorMessage = computed<string | null>(() => {
    const err = this.resource.error();
    if (!err) {
      return null;
    }
    if (isApiError(err)) {
      return err.detail;
    }
    return "Nepodařilo se načíst statistiku.";
  });

  protected barWidth(percent: number): string {
    const clamped = Math.max(0, Math.min(100, percent));
    return `${clamped}%`;
  }
}
