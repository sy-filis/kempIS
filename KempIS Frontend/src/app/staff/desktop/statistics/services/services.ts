import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { MessageModule } from "primeng/message";
import { TableModule } from "primeng/table";

import { ApiClient } from "../../../../core/api/api-client";
import { isApiError } from "../../../../core/api/api-error";
import { dateToIso } from "../../../../shared/date-iso";
import { formatPercent } from "../../../../shared/format-percent";
import type {
  ServiceRevenueGroup,
  ServiceRevenueStatsResponse,
} from "../../../api/stats.types";
import { formatCzk } from "../../bills/bills-data";

function rangeDays(from: Date, to: Date): number {
  return Math.floor((to.getTime() - from.getTime()) / 86_400_000) + 1;
}

const SERVICE_GROUP_LABELS_CS: Record<string, string> = {
  Persons: "Osoby",
  Vehicles: "Vozidla",
  MotorHomes: "Obytné vozy",
  Tents: "Stany",
  Meals: "Strava",
  Spots: "Místa",
  RecreationFees: "Rekreační poplatky",
  Others: "Ostatní",
};

@Component({
  selector: "kemp-is-stats-services",
  imports: [ButtonModule, MessageModule, TableModule],
  templateUrl: "./services.html",
  styleUrl: "./services.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsServicesPanel {
  private readonly apiClient = inject(ApiClient);

  protected readonly formatCzk = formatCzk;
  protected readonly formatPercent = formatPercent;
  protected readonly serviceGroupLabel = (key: string): string =>
    SERVICE_GROUP_LABELS_CS[key] ?? key;

  readonly range = input<Date[]>([]);

  protected readonly expandedRows = signal<Record<string, boolean>>({});

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

  protected readonly resource = httpResource<ServiceRevenueStatsResponse>(
    () => {
      const q = this.query();
      if (!q) {
        return undefined;
      }
      const params = new URLSearchParams({ from: q.from, to: q.to });
      return `${this.apiClient.url("/stats/services")}?${params.toString()}`;
    }
  );

  protected readonly groups = computed<ServiceRevenueGroup[]>(() =>
    this.resource.hasValue() ? [...this.resource.value().groups] : []
  );

  protected readonly totals = computed<{
    totalNet: number;
    totalVat: number;
    totalGross: number;
  } | null>(() => {
    if (!this.resource.hasValue()) {
      return null;
    }
    const v = this.resource.value();
    return {
      totalNet: v.totalNet,
      totalVat: v.totalVat,
      totalGross: v.totalGross,
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
}
