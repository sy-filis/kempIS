import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { CheckboxModule } from "primeng/checkbox";
import { TagModule } from "primeng/tag";

import {
  InvoiceStatus,
  type ReservationDetailInvoice,
} from "../../../api/reservations.types";
import { BillState } from "../bill-state";

type InvoiceVm = {
  readonly invoice: ReservationDetailInvoice;
  readonly checked: boolean;
  readonly statusLabel: string;
  readonly statusSeverity: "success" | "warn" | "secondary" | "danger";
  readonly statusIcon: string;
};

@Component({
  selector: "kemp-is-bill-step5-invoices",
  imports: [FormsModule, CheckboxModule, TagModule],
  templateUrl: "./step5-invoices.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step5Invoices {
  private readonly billState = inject(BillState);

  protected readonly rows = computed<readonly InvoiceVm[]>(() => {
    const linked = this.billState.linkedInvoiceIds();
    return this.billState.reservationInvoices().map(invoice => ({
      invoice,
      checked: linked.has(invoice.id),
      ...presentStatus(invoice.status),
    }));
  });

  protected readonly linkedCount = computed(
    () => this.billState.linkedInvoiceIds().size
  );

  protected toggleLinked(id: string): void {
    this.billState.linkedInvoiceIds.update(set => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected formatDate(iso: string | null): string {
    if (!iso) {
      return "—";
    }
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
    if (!m) {
      return iso;
    }
    return `${Number(m[3])}. ${Number(m[2])}. ${Number(m[1])}`;
  }
}

function presentStatus(status: InvoiceStatus): {
  statusLabel: string;
  statusSeverity: "success" | "warn" | "secondary" | "danger";
  statusIcon: string;
} {
  switch (status) {
    case InvoiceStatus.Paid:
      return {
        statusLabel: "Uhrazeno",
        statusSeverity: "success",
        statusIcon: "pi pi-check",
      };
    case InvoiceStatus.Created:
      return {
        statusLabel: "Vystaveno",
        statusSeverity: "warn",
        statusIcon: "pi pi-clock",
      };
    case InvoiceStatus.Pending:
      return {
        statusLabel: "Návrh",
        statusSeverity: "secondary",
        statusIcon: "pi pi-pencil",
      };
    case InvoiceStatus.Cancelled:
      return {
        statusLabel: "Stornováno",
        statusSeverity: "danger",
        statusIcon: "pi pi-times",
      };
    default:
      return {
        statusLabel: "—",
        statusSeverity: "secondary",
        statusIcon: "pi pi-circle",
      };
  }
}
