import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";

import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";

import {
  type BillSummary,
  checkInSortKey,
  formatCzk,
  formatIssued,
  formatStay,
  issuedSortKey,
  kindIcon,
  kindLabel,
  kindSeverity,
} from "./bills-data";
import { ApiClient } from "../../../core/api/api-client";
import { dateToIso } from "../../../shared/date-iso";
import type { FinancialClosingSummary } from "../../api/financial-closings.types";

const SEARCH_DEBOUNCE_MS = 250;

function startOfMonth(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

function endOfMonth(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth() + 1, 0);
}

@Component({
  selector: "kemp-is-bills",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageModule,
    TableModule,
    TagModule,
  ],
  templateUrl: "./bills.page.html",
  styleUrl: "./bills.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BillsPage {
  private readonly apiClient = inject(ApiClient);
  private readonly router = inject(Router);

  protected onOpenBill(id: string): void {
    void this.router.navigate(["/staff/auth/desktop/bills", id]);
  }

  protected readonly from = signal<Date | null>(startOfMonth());
  protected readonly to = signal<Date | null>(endOfMonth());
  protected readonly search = signal<string>("");
  private readonly searchDebounced = signal<string>("");

  constructor() {
    effect(onCleanup => {
      const v = this.search();
      const id = setTimeout(
        () => this.searchDebounced.set(v),
        SEARCH_DEBOUNCE_MS
      );
      onCleanup(() => clearTimeout(id));
    });
  }

  protected readonly resource = httpResource<readonly BillSummary[]>(() => {
    const f = this.from();
    const t = this.to();
    const params = new URLSearchParams();
    if (f) {
      params.set("from", dateToIso(f));
    }
    if (t) {
      params.set("to", dateToIso(t));
    }
    const query = params.toString();
    return query.length > 0
      ? `${this.apiClient.url("/bills")}?${query}`
      : this.apiClient.url("/bills");
  });

  /** Whole-year range so the lookup covers closings just outside the bill date filter. */
  private readonly closingsResource = httpResource<
    readonly FinancialClosingSummary[]
  >(() => {
    const year = new Date().getFullYear();
    return `${this.apiClient.url("/financial-closings")}?from=${year}-01-01&to=${year}-12-31`;
  });

  protected readonly closingNumberById = computed<ReadonlyMap<string, number>>(
    () => {
      if (!this.closingsResource.hasValue()) {
        return new Map();
      }
      return new Map(
        this.closingsResource
          .value()
          .map(c => [c.id, c.financialClosingId] as const)
      );
    }
  );

  protected closingNumber(closingId: string | null): string {
    if (closingId === null) {
      return "—";
    }
    const n = this.closingNumberById().get(closingId);
    return n === undefined ? "…" : `#${n}`;
  }

  /** Free-text filter is local: /bills has no search param. */
  protected readonly rows = computed<BillSummary[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    const all = this.resource.value();
    const term = this.searchDebounced().trim().toLocaleLowerCase("cs-CZ");
    if (term.length === 0) {
      return [...all];
    }
    return all.filter(b => b.number.toLocaleLowerCase("cs-CZ").includes(term));
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly hasError = computed(
    () => this.resource.error() !== undefined
  );

  protected readonly count = computed(() => this.rows().length);

  protected readonly totalAmount = computed(() =>
    this.rows().reduce((sum, b) => sum + b.amount, 0)
  );

  protected readonly minToDate = computed(() => this.from() ?? undefined);

  protected onRetry(): void {
    this.resource.reload();
  }

  protected readonly kindLabel = kindLabel;
  protected readonly kindSeverity = kindSeverity;
  protected readonly kindIcon = kindIcon;
  protected readonly formatCzk = formatCzk;
  protected readonly formatStay = formatStay;
  protected readonly formatIssued = formatIssued;
  protected readonly issuedSortKey = issuedSortKey;
  protected readonly checkInSortKey = checkInSortKey;
}
