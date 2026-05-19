import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { InputNumberModule } from "primeng/inputnumber";

import { KNOWN_SERVICE_IDS } from "../../../../core/services/known-service-ids";
import { ServicesStore } from "../../../../core/services/services.store";
import { type MealDay } from "../bill-data";
import { BillState } from "../bill-state";

type MealKind = "b" | "l" | "lp" | "d";

@Component({
  selector: "kemp-is-bill-step4-meals",
  imports: [FormsModule, InputNumberModule],
  templateUrl: "./step4-meals.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step4Meals {
  private readonly billState = inject(BillState);
  private readonly servicesStore = inject(ServicesStore);

  protected readonly prices = computed(() => ({
    b: this.servicesStore.byId(KNOWN_SERVICE_IDS.breakfast)?.basePrice ?? 0,
    l: this.servicesStore.byId(KNOWN_SERVICE_IDS.lunch)?.basePrice ?? 0,
    lp: this.servicesStore.byId(KNOWN_SERVICE_IDS.lunchPackage)?.basePrice ?? 0,
    d: this.servicesStore.byId(KNOWN_SERVICE_IDS.dinner)?.basePrice ?? 0,
  }));

  protected readonly days = this.billState.meals;

  protected readonly totals = computed(() => {
    const list = this.days();
    const b = list.reduce((s, d) => s + d.b, 0);
    const l = list.reduce((s, d) => s + d.l, 0);
    const lp = list.reduce((s, d) => s + d.lp, 0);
    const d = list.reduce((s, dd) => s + dd.d, 0);
    return { b, l, lp, d, all: b + l + lp + d };
  });

  protected readonly grandTotal = computed(() => {
    const p = this.prices();
    return this.days().reduce(
      (sum, d) => sum + d.b * p.b + d.l * p.l + d.lp * p.lp + d.d * p.d,
      0
    );
  });

  protected dayTotal(day: MealDay): number {
    const p = this.prices();
    return day.b * p.b + day.l * p.l + day.lp * p.lp + day.d * p.d;
  }

  protected dayLabel(idx: number): string {
    if (idx === 0) {
      return "den příjezdu";
    }
    if (idx === this.days().length - 1) {
      return "den odjezdu";
    }
    return `${idx + 1}. den`;
  }

  protected updateMeal(dayIdx: number, kind: MealKind, value: number): void {
    this.days.update(list =>
      list.map((d, i) =>
        i === dayIdx ? { ...d, [kind]: Math.max(0, value) } : d
      )
    );
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }
}
