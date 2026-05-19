import { formatDateRange } from "../../../shared/format-date-range";

export function formatPeriod(from: string, to: string): string {
  return formatDateRange(from, to);
}
