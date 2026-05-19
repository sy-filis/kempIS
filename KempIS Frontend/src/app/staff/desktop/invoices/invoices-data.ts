import { formatDateRange } from "../../../shared/format-date-range";
import { InvoiceStatus, type InvoiceSummary } from "../../api/invoices.types";

const STATUS_LABELS_CS: Record<InvoiceStatus, string> = {
  [InvoiceStatus.Draft]: "Návrh",
  [InvoiceStatus.Created]: "Vystaveno",
  [InvoiceStatus.Paid]: "Zaplaceno",
};

const STATUS_SEVERITY: Record<
  InvoiceStatus,
  "info" | "warn" | "success" | "secondary"
> = {
  [InvoiceStatus.Draft]: "secondary",
  [InvoiceStatus.Created]: "warn",
  [InvoiceStatus.Paid]: "success",
};

const STATUS_ICON: Record<InvoiceStatus, string> = {
  [InvoiceStatus.Draft]: "pi pi-file",
  [InvoiceStatus.Created]: "pi pi-clock",
  [InvoiceStatus.Paid]: "pi pi-check",
};

export function statusLabel(status: InvoiceStatus): string {
  return STATUS_LABELS_CS[status];
}

export function statusSeverity(
  status: InvoiceStatus
): "info" | "warn" | "success" | "secondary" {
  return STATUS_SEVERITY[status];
}

export function statusIcon(status: InvoiceStatus): string {
  return STATUS_ICON[status];
}

const CZK = new Intl.NumberFormat("cs-CZ", {
  style: "currency",
  currency: "CZK",
  maximumFractionDigits: 0,
});

export function formatCzk(amount: number): string {
  return CZK.format(amount);
}

function formatTimestamp(iso: string | null): string {
  if (iso === null) {
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

export function formatIssued(iso: string): string {
  return formatTimestamp(iso);
}

export function formatPaid(iso: string | null): string {
  return formatTimestamp(iso);
}

export function formatDue(iso: string | null): string {
  return formatTimestamp(iso);
}

export function timestampSortKey(iso: string | null): number {
  if (iso === null) {
    return 0;
  }
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? 0 : d.getTime();
}

export { InvoiceStatus, formatDateRange };
export type { InvoiceSummary };
