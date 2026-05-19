import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  ViewEncapsulation,
} from "@angular/core";
import { Router } from "@angular/router";

import { ButtonModule } from "primeng/button";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";

import {
  BillKind,
  InvoiceStatus,
  PaymentType,
  type ReservationDetailBill,
  type ReservationDetailInvoice,
} from "../../../../api/reservations.types";

@Component({
  selector: "kemp-is-reservation-step-doklady",
  imports: [ButtonModule, TableModule, TagModule],
  templateUrl: "./step-doklady.html",
  styleUrl: "./step-doklady.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class StepDoklady {
  private readonly router = inject(Router);

  readonly reservationId = input<string | null>(null);
  readonly invoices = input.required<readonly ReservationDetailInvoice[]>();
  readonly bills = input.required<readonly ReservationDetailBill[]>();

  // p-table needs a mutable array for [value]; readonly inputs trip variance.
  protected readonly invoiceRows = computed<ReservationDetailInvoice[]>(() => [
    ...this.invoices(),
  ]);

  protected readonly billRows = computed<ReservationDetailBill[]>(() => [
    ...this.bills(),
  ]);

  protected onCreateInvoice(): void {
    const id = this.reservationId();
    if (!id) {
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/invoices/new"], {
      queryParams: { reservationId: id },
    });
  }

  protected onCreateBill(): void {
    const id = this.reservationId();
    if (!id) {
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/bill/new"], {
      queryParams: { reservationId: id },
    });
  }

  protected onOpenBill(billId: string): void {
    void this.router.navigate(["/staff/auth/desktop/bills", billId]);
  }

  protected invoiceStatusLabel(status: InvoiceStatus): string {
    switch (status) {
      case InvoiceStatus.Pending:
        return "Návrh";
      case InvoiceStatus.Created:
        return "Vystaveno";
      case InvoiceStatus.Paid:
        return "Uhrazeno";
      case InvoiceStatus.Cancelled:
        return "Zrušeno";
    }
  }

  protected invoiceStatusSeverity(
    status: InvoiceStatus
  ): "secondary" | "warn" | "success" | "danger" {
    switch (status) {
      case InvoiceStatus.Pending:
        return "secondary";
      case InvoiceStatus.Created:
        return "warn";
      case InvoiceStatus.Paid:
        return "success";
      case InvoiceStatus.Cancelled:
        return "danger";
    }
  }

  protected invoiceStatusIcon(status: InvoiceStatus): string {
    switch (status) {
      case InvoiceStatus.Pending:
        return "pi pi-pencil";
      case InvoiceStatus.Created:
        return "pi pi-clock";
      case InvoiceStatus.Paid:
        return "pi pi-check";
      case InvoiceStatus.Cancelled:
        return "pi pi-times";
    }
  }

  protected billKindLabel(kind: BillKind): string {
    switch (kind) {
      case BillKind.Reservation:
        return "Pobyt";
      case BillKind.Repair:
        return "Oprava";
      case BillKind.Other:
        return "Ostatní";
    }
  }

  protected billKindSeverity(kind: BillKind): "info" | "warn" | "secondary" {
    switch (kind) {
      case BillKind.Reservation:
        return "info";
      case BillKind.Repair:
        return "warn";
      case BillKind.Other:
        return "secondary";
    }
  }

  protected paymentTypeLabel(type: PaymentType): string {
    switch (type) {
      case PaymentType.Cash:
        return "Hotově";
      case PaymentType.Card:
        return "Kartou";
    }
  }

  protected formatCzk(n: number): string {
    return `${n.toLocaleString("cs-CZ")} Kč`;
  }

  protected formatDate(iso: string | null): string {
    if (!iso) {
      return "—";
    }
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) {
      return iso;
    }
    return d.toLocaleDateString("cs-CZ", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  }
}
