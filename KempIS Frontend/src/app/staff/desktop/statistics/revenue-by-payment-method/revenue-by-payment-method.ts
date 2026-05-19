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
  RevenueByPaymentMethodResponse,
  RevenueByPaymentMethodRow,
} from "../../../api/stats.types";
import { formatCzk } from "../../bills/bills-data";

function rangeDays(from: Date, to: Date): number {
  return Math.floor((to.getTime() - from.getTime()) / 86_400_000) + 1;
}

const PAYMENT_TYPE_LABELS_CS: Record<string, string> = {
  Cash: "Hotově",
  Card: "Kartou",
};

@Component({
  selector: "kemp-is-stats-revenue-by-payment-method",
  imports: [MessageModule, TableModule],
  templateUrl: "./revenue-by-payment-method.html",
  styleUrl: "./revenue-by-payment-method.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsRevenueByPaymentMethodPanel {
  private readonly apiClient = inject(ApiClient);

  protected readonly formatCzk = formatCzk;
  protected readonly formatPercent = formatPercent;
  protected paymentTypeLabel(key: string): string {
    return PAYMENT_TYPE_LABELS_CS[key] ?? key;
  }

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

  protected readonly resource = httpResource<RevenueByPaymentMethodResponse>(
    () => {
      const q = this.query();
      if (!q) {
        return undefined;
      }
      const params = new URLSearchParams({ from: q.from, to: q.to });
      return `${this.apiClient.url("/stats/revenue/by-payment-method")}?${params.toString()}`;
    }
  );

  protected readonly rows = computed<RevenueByPaymentMethodRow[]>(() =>
    this.resource.hasValue() ? [...this.resource.value().rows] : []
  );

  protected readonly totals = computed<{
    totalBillCount: number;
    totalGross: number;
  } | null>(() => {
    if (!this.resource.hasValue()) {
      return null;
    }
    const v = this.resource.value();
    return {
      totalBillCount: v.totalBillCount,
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

  protected barWidth(percent: number): string {
    const clamped = Math.max(0, Math.min(100, percent));
    return `${clamped}%`;
  }
}
