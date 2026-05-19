import { isoToDate } from "../../../shared/date-iso";
import { formatDateRange } from "../../../shared/format-date-range";
import { BillKind, type BillSummary } from "../../api/bills.types";

const KIND_LABELS_CS: Record<BillKind, string> = {
  [BillKind.Regular]: "Běžná",
  [BillKind.Repair]: "Opravná",
};

const KIND_SEVERITY: Record<BillKind, "info" | "warn"> = {
  [BillKind.Regular]: "info",
  [BillKind.Repair]: "warn",
};

const KIND_ICON: Record<BillKind, string> = {
  [BillKind.Regular]: "pi pi-receipt",
  [BillKind.Repair]: "pi pi-wrench",
};

export function kindLabel(kind: BillKind): string {
  return KIND_LABELS_CS[kind];
}

export function kindSeverity(kind: BillKind): "info" | "warn" {
  return KIND_SEVERITY[kind];
}

export function kindIcon(kind: BillKind): string {
  return KIND_ICON[kind];
}

const CZK = new Intl.NumberFormat("cs-CZ", {
  style: "currency",
  currency: "CZK",
  maximumFractionDigits: 0,
});

export function formatCzk(amount: number): string {
  return CZK.format(amount);
}

export function formatStay(from: string, to: string): string {
  return formatDateRange(from, to);
}

export function formatIssued(iso: string): string {
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

/** Default string sort would order DD.MM.YYYY incorrectly. */
export function issuedSortKey(iso: string): number {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? 0 : d.getTime();
}

export function checkInSortKey(iso: string): number {
  const d = isoToDate(iso);
  return d ? d.getTime() : 0;
}

export type { BillSummary };
export { BillKind };
