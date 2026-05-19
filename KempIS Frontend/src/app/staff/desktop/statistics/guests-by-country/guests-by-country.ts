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
  GuestStatsByCountryResponse,
  GuestStatsByCountryRow,
} from "../../../api/stats.types";

function rangeDays(from: Date, to: Date): number {
  return Math.floor((to.getTime() - from.getTime()) / 86_400_000) + 1;
}

@Component({
  selector: "kemp-is-stats-guests-by-country",
  imports: [MessageModule, TableModule],
  templateUrl: "./guests-by-country.html",
  styleUrl: "./guests-by-country.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsGuestsByCountryPanel {
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

  protected readonly resource = httpResource<GuestStatsByCountryResponse>(
    () => {
      const q = this.query();
      if (!q) {
        return undefined;
      }
      const params = new URLSearchParams({ from: q.from, to: q.to });
      return `${this.apiClient.url("/stats/guests/by-country")}?${params.toString()}`;
    }
  );

  protected readonly rows = computed<GuestStatsByCountryRow[]>(() =>
    this.resource.hasValue() ? [...this.resource.value().rows] : []
  );

  protected readonly totals = computed<{
    totalGuests: number;
    totalPersonNights: number;
  } | null>(() => {
    if (!this.resource.hasValue()) {
      return null;
    }
    const v = this.resource.value();
    return {
      totalGuests: v.totalGuests,
      totalPersonNights: v.totalPersonNights,
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
