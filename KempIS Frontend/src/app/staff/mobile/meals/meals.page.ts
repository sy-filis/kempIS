import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";

import { ApiClient } from "../../../core/api/api-client";
import { dateToIso, isoToDate } from "../../../shared/date-iso";
import type { MealResponse } from "../../api/meals.types";
import type { Reservation } from "../../api/reservations.types";
import {
  buildGroupsFromApi,
  type DayKey,
  dayMetaFor,
  DIET_VARIANTS,
  type DietVariant,
  type Group,
  MEAL_TYPES,
  type MealKey,
  type MealRecord,
  type MealType,
  sumVariants,
  type VariantKey,
  type WeekDay,
} from "../../desktop/meals/meals-data";
import { ScreenHeader } from "../shared/screen-header";

type DietCounts = Record<VariantKey, number>;

type MealPickup = {
  readonly groupId: string;
  readonly groupName: string;
  readonly color: string;
  readonly time: string;
  readonly total: number;
  readonly diet: DietCounts;
};

type MealBlock = {
  readonly key: MealKey;
  readonly label: string;
  readonly icon: string;
  readonly timeRange: string;
  readonly total: number;
  readonly diet: DietCounts;
  readonly pickups: readonly MealPickup[];
};

const VARIANT_KEYS: readonly VariantKey[] = DIET_VARIANTS.map(v => v.key);
const EMPTY_DIET: DietCounts = VARIANT_KEYS.reduce(
  (acc, k) => ({ ...acc, [k]: 0 }),
  {} as DietCounts
);
const MONTHS_GENITIVE = [
  "ledna",
  "února",
  "března",
  "dubna",
  "května",
  "června",
  "července",
  "srpna",
  "září",
  "října",
  "listopadu",
  "prosince",
] as const;

@Component({
  selector: "kemp-is-staff-meals",
  imports: [FormsModule, ButtonModule, DatePickerModule, ScreenHeader],
  templateUrl: "./meals.page.html",
  styleUrl: "./meals.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealsPage {
  private readonly apiClient = inject(ApiClient);

  protected readonly mealTypes: readonly MealType[] = MEAL_TYPES;
  protected readonly diets: readonly DietVariant[] = DIET_VARIANTS;

  protected readonly selectedDate = signal<string>(dateToIso(new Date()));
  protected readonly activeMeal = signal<MealKey>("S");

  protected readonly pickerDate = computed(() =>
    isoToDate(this.selectedDate())
  );

  // buildGroupsFromApi expects a week range; mobile renders one day at a time.
  private readonly weekDays = computed<readonly WeekDay[]>(() => {
    const meta = dayMetaFor(this.selectedDate());
    return meta ? [meta] : [];
  });

  protected readonly selectedDayMeta = computed<WeekDay | null>(() =>
    dayMetaFor(this.selectedDate())
  );

  protected readonly subtitle = computed(() => {
    const d = this.selectedDayMeta();
    if (!d) {
      return "";
    }
    const month = MONTHS_GENITIVE[d.m - 1] ?? "";
    const yearMatch = /^(\d{4})/.exec(this.selectedDate());
    const year = yearMatch ? yearMatch[1] : "";
    return `${d.name} ${d.d}. ${month} ${year}`;
  });

  private readonly mealsResource = httpResource<readonly MealResponse[]>(() => {
    const date = this.selectedDate();
    return date
      ? `${this.apiClient.url("/meals")}?from=${date}&to=${date}`
      : undefined;
  });

  private readonly reservationsResource = httpResource<readonly Reservation[]>(
    () => {
      const date = this.selectedDate();
      return date
        ? `${this.apiClient.url("/reservations")}?from=${date}&to=${date}`
        : undefined;
    }
  );

  private readonly groups = computed<readonly Group[]>(() => {
    const meals = this.mealsResource.hasValue()
      ? this.mealsResource.value()
      : [];
    const reservations = this.reservationsResource.hasValue()
      ? this.reservationsResource.value()
      : [];
    return buildGroupsFromApi(meals, reservations, this.weekDays());
  });

  protected readonly blocks = computed<readonly MealBlock[]>(() =>
    this.mealTypes.map(meal => this.buildBlock(meal))
  );

  protected readonly mealTotals = computed<Record<MealKey, number>>(() => {
    const totals = { S: 0, O: 0, OB: 0, V: 0 } as Record<MealKey, number>;
    for (const block of this.blocks()) {
      totals[block.key] = block.total;
    }
    return totals;
  });

  protected readonly activeBlock = computed<MealBlock>(() => {
    const meal = this.activeMeal();
    return (
      this.blocks().find(b => b.key === meal) ?? (this.blocks()[0] as MealBlock)
    );
  });

  protected setActiveMeal(key: MealKey): void {
    this.activeMeal.set(key);
  }

  protected onDateChange(value: Date | null): void {
    if (value) {
      this.selectedDate.set(dateToIso(value));
    }
  }

  private buildBlock(meal: MealType): MealBlock {
    const dayKey = this.selectedDayMeta()?.key;
    const pickups: MealPickup[] = [];
    const diet: DietCounts = { ...EMPTY_DIET };
    let total = 0;

    if (dayKey) {
      for (const group of this.groups()) {
        const pickup = this.buildPickup(group, dayKey, meal.key);
        if (!pickup) {
          continue;
        }
        pickups.push(pickup);
        total += pickup.total;
        for (const k of VARIANT_KEYS) {
          diet[k] += pickup.diet[k];
        }
      }
    }

    pickups.sort((a, b) => a.time.localeCompare(b.time));

    return {
      key: meal.key,
      label: meal.label,
      icon: meal.icon,
      timeRange: this.timeRangeFor(meal.key),
      total,
      diet,
      pickups,
    };
  }

  private buildPickup(
    group: Group,
    dayKey: DayKey,
    mealKey: MealKey
  ): MealPickup | null {
    const day = group.days[dayKey];
    if (!day) {
      return null;
    }
    const rec: MealRecord = day[mealKey];
    const total = sumVariants(rec);
    if (total === 0 || !rec.t) {
      return null;
    }
    const diet = VARIANT_KEYS.reduce(
      (acc, k) => ({ ...acc, [k]: rec[k] }),
      {} as DietCounts
    );
    return {
      groupId: group.id,
      groupName: group.name,
      color: group.color,
      time: rec.t,
      total,
      diet,
    };
  }

  private timeRangeFor(key: MealKey): string {
    switch (key) {
      case "S":
        return "07:30 - 08:30";
      case "O":
        return "12:00 - 13:00";
      case "OB":
        return "11:00 - 12:00";
      case "V":
        return "18:00 - 19:00";
    }
  }
}
