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
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";

import {
  formatCzk,
  formatIssued,
  formatPaid,
  type InvoiceSummary,
  statusIcon,
  statusLabel,
  statusSeverity,
  timestampSortKey,
} from "./invoices-data";
import { ApiClient } from "../../../core/api/api-client";
import { InvoiceStateFilter } from "../../api/invoices.types";

type StateOption = {
  label: string;
  value: InvoiceStateFilter | null;
};

const STATE_OPTIONS: StateOption[] = [
  { label: "Všechny", value: null },
  { label: "Návrhy", value: InvoiceStateFilter.Draft },
  { label: "Vystavené", value: InvoiceStateFilter.Created },
  { label: "Zaplacené", value: InvoiceStateFilter.Paid },
  { label: "Po splatnosti", value: InvoiceStateFilter.AfterDue },
];

const SEARCH_DEBOUNCE_MS = 250;

function startOfMonth(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

function endOfMonth(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59, 999);
}

@Component({
  selector: "kemp-is-invoices",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: "./invoices.page.html",
  styleUrl: "./invoices.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoicesPage {
  private readonly apiClient = inject(ApiClient);
  private readonly router = inject(Router);

  protected readonly from = signal<Date | null>(startOfMonth());
  protected readonly to = signal<Date | null>(endOfMonth());
  protected readonly state = signal<InvoiceStateFilter | null>(null);
  protected readonly search = signal<string>("");
  private readonly searchDebounced = signal<string>("");

  protected readonly stateOptions = STATE_OPTIONS;

  constructor() {
    effect(onCleanup => {
      const v = this.search();
      const id = setTimeout(
        () => this.searchDebounced.set(v),
        SEARCH_DEBOUNCE_MS
      );
      onCleanup(() => clearTimeout(id));
    });

    // Drafts and overdue invoices must surface regardless of reservation period.
    effect(() => {
      const s = this.state();
      if (s === InvoiceStateFilter.Draft || s === InvoiceStateFilter.AfterDue) {
        this.from.set(null);
        this.to.set(null);
      }
    });
  }

  protected readonly resource = httpResource<readonly InvoiceSummary[]>(() => {
    const f = this.from();
    const t = this.to();
    const s = this.state();
    const params = new URLSearchParams();
    if (f) {
      params.set("from", f.toISOString());
    }
    if (t) {
      params.set("to", t.toISOString());
    }
    if (s) {
      params.set("state", s);
    }
    const query = params.toString();
    return query.length > 0
      ? `${this.apiClient.url("/invoices")}?${query}`
      : this.apiClient.url("/invoices");
  });

  /** Free-text filter is local: the list endpoint has no search param. */
  protected readonly rows = computed<InvoiceSummary[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    const all = this.resource.value();
    const term = this.searchDebounced().trim().toLocaleLowerCase("cs-CZ");
    if (term.length === 0) {
      return [...all];
    }
    return all.filter(i =>
      (i.number ?? "").toLocaleLowerCase("cs-CZ").includes(term)
    );
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly hasError = computed(
    () => this.resource.error() !== undefined
  );

  protected readonly count = computed(() => this.rows().length);

  protected readonly totalAmount = computed(() =>
    this.rows().reduce((sum, i) => sum + i.totalAmount, 0)
  );

  protected readonly minToDate = computed(() => this.from() ?? undefined);

  protected onRetry(): void {
    this.resource.reload();
  }

  protected onRowClick(invoice: InvoiceSummary): void {
    void this.router.navigate(["/staff/auth/desktop/invoices", invoice.id]);
  }

  protected readonly statusLabel = statusLabel;
  protected readonly statusSeverity = statusSeverity;
  protected readonly statusIcon = statusIcon;
  protected readonly formatCzk = formatCzk;
  protected readonly formatIssued = formatIssued;
  protected readonly formatPaid = formatPaid;
  protected readonly timestampSortKey = timestampSortKey;
}
