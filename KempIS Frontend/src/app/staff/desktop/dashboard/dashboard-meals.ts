import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";

import { TagModule } from "primeng/tag";

import { ApiClient } from "../../../core/api/api-client";
import { dateToIso } from "../../../shared/date-iso";
import type { MealAmountDto, MealResponse } from "../../api/meals.types";

type MealStatus = "done" | "next" | "planned";

type MealRow = {
  readonly meal: string;
  readonly count: number;
  readonly served: number;
  readonly status: MealStatus;
  readonly time: string | null;
};

const MEAL_DEFS = [
  { key: "breakfast", label: "Snídaně", endHour: 9.5 },
  { key: "lunch", label: "Oběd", endHour: 13.5 },
  { key: "dinner", label: "Večeře", endHour: 20 },
] as const;

function sumAmount(a: MealAmountDto): number {
  return (
    a.normal +
    a.glutenFree +
    a.lactoseFree +
    a.vegetarian +
    a.glutenFreeLactoseFree +
    a.glutenFreeVegetarian +
    a.lactoseFreeVegetarian +
    a.glutenFreeLactoseFreeVegetarian
  );
}

function trimTime(at: string | null): string | null {
  if (at === null) {
    return null;
  }
  const match = /^(\d{2}):(\d{2})/.exec(at);
  return match ? `${match[1]}:${match[2]}` : null;
}

function aggregateTime(amounts: readonly MealAmountDto[]): string | null {
  const times: string[] = [];
  for (const a of amounts) {
    if (sumAmount(a) === 0) {
      continue;
    }
    const t = trimTime(a.at);
    if (t !== null) {
      times.push(t);
    }
  }
  if (times.length === 0) {
    return null;
  }
  times.sort();
  const lo = times[0]!;
  const hi = times[times.length - 1]!;
  return lo === hi ? lo : `${lo} – ${hi}`;
}

@Component({
  selector: "kemp-is-dash-meals",
  imports: [TagModule],
  templateUrl: "./dashboard-meals.html",
  styleUrl: "./dashboard-meals.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashMealsPanel {
  private readonly apiClient = inject(ApiClient);

  private readonly today = dateToIso(new Date());

  private readonly mealsToday = httpResource<readonly MealResponse[]>(() =>
    this.apiClient.url(`/meals?from=${this.today}&to=${this.today}`)
  );

  protected readonly meals = computed<readonly MealRow[]>(() => {
    const list = this.mealsToday.hasValue() ? this.mealsToday.value() : [];
    const totals = list.reduce(
      (acc, m) => {
        acc.breakfast += sumAmount(m.breakfast);
        acc.lunch += sumAmount(m.lunch) + sumAmount(m.lunchPackage);
        acc.dinner += sumAmount(m.dinner);
        return acc;
      },
      { breakfast: 0, lunch: 0, dinner: 0 }
    );

    const times = {
      breakfast: aggregateTime(list.map(m => m.breakfast)),
      lunch: aggregateTime([
        ...list.map(m => m.lunch),
        ...list.map(m => m.lunchPackage),
      ]),
      dinner: aggregateTime(list.map(m => m.dinner)),
    };

    const now = new Date();
    const hour = now.getHours() + now.getMinutes() / 60;

    const firstUnfinished = MEAL_DEFS.find(d => hour < d.endHour)?.key ?? null;

    return MEAL_DEFS.map(d => {
      const count = totals[d.key];
      const status: MealStatus =
        hour >= d.endHour
          ? "done"
          : d.key === firstUnfinished
            ? "next"
            : "planned";
      return {
        meal: d.label,
        count,
        served: status === "done" ? count : 0,
        status,
        time: times[d.key],
      };
    });
  });

  protected readonly hasMeals = computed<boolean>(() =>
    this.meals().some(m => m.count > 0)
  );
}
