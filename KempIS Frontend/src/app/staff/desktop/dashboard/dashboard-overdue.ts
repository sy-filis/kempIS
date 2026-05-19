import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";

import { TagModule } from "primeng/tag";

import { fmtCzk } from "./dashboard-data";
import { ApiClient } from "../../../core/api/api-client";
import type { InvoiceSummary } from "../../api/invoices.types";

type OverdueRow = {
  readonly ref: string;
  readonly customer: string;
  readonly amount: number;
  readonly daysOverdue: number;
};

const MS_PER_DAY = 1000 * 60 * 60 * 24;

@Component({
  selector: "kemp-is-dash-overdue",
  imports: [TagModule],
  templateUrl: "./dashboard-overdue.html",
  styleUrl: "./dashboard-overdue.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashOverduePanel {
  private readonly apiClient = inject(ApiClient);

  protected readonly fmtCzk = fmtCzk;

  private readonly afterDue = httpResource<readonly InvoiceSummary[]>(() =>
    this.apiClient.url("/invoices?state=AfterDue")
  );

  protected readonly invoices = computed<readonly OverdueRow[]>(() => {
    if (!this.afterDue.hasValue()) {
      return [];
    }
    const now = Date.now();
    return this.afterDue
      .value()
      .map(i => {
        if (i.dueTo === null) {
          return null;
        }
        const due = Date.parse(i.dueTo);
        const daysOverdue = Math.max(1, Math.floor((now - due) / MS_PER_DAY));
        return { invoice: i, daysOverdue };
      })
      .filter(
        (x): x is { invoice: InvoiceSummary; daysOverdue: number } => x !== null
      )
      .sort((a, b) => b.daysOverdue - a.daysOverdue)
      .map(({ invoice, daysOverdue }) => ({
        ref: invoice.number ?? invoice.id.slice(0, 8),
        customer: invoice.reservation.number,
        amount: invoice.totalAmount,
        daysOverdue,
      }));
  });

  protected readonly total = computed(() =>
    this.invoices().reduce((sum, inv) => sum + inv.amount, 0)
  );
}
