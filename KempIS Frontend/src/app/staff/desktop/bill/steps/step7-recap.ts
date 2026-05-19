import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";

import type { RecapRow } from "../bill-data";
import { BillState } from "../bill-state";

@Component({
  selector: "kemp-is-bill-step7-recap",
  imports: [FormsModule, ButtonModule, InputNumberModule],
  templateUrl: "./step7-recap.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step7Recap {
  private readonly billState = inject(BillState);

  private readonly sourceRowsById = computed<ReadonlyMap<string, RecapRow>>(
    () => new Map(this.billState.recapRows().map(r => [r.id, r]))
  );

  protected readonly rows = this.billState.finalRecapRows;

  protected isModified(
    rowId: string,
    field: "qty" | "days" | "price"
  ): boolean {
    const override = this.billState.recapOverrides().get(rowId);
    if (!override || override[field] === undefined) {
      return false;
    }
    const source = this.sourceRowsById().get(rowId);
    if (!source) {
      return true;
    }
    return override[field] !== source[field];
  }

  protected readonly grandTotal = this.billState.grandTotal;

  protected updateField(
    id: string,
    field: "service" | "days" | "price" | "qty",
    value: string | number
  ): void {
    this.billState.recapOverrides.update(map => {
      const next = new Map(map);
      const current = next.get(id) ?? {};
      if (field === "service") {
        next.set(id, { ...current, service: String(value) });
      } else {
        const n = typeof value === "number" ? value : Number(value) || 0;
        next.set(id, { ...current, [field]: Math.max(0, n) });
      }
      return next;
    });
  }

  protected lineTotal(row: RecapRow): number {
    return row.days * row.price * row.qty;
  }

  protected pad(idx: number): string {
    return String(idx + 1).padStart(2, "0");
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }
}
