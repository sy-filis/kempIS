import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  ViewEncapsulation,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import type { MenuItem } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { MenuModule } from "primeng/menu";

import { KNOWN_SERVICE_IDS } from "../../../../../core/services/known-service-ids";
import { ServicesStore } from "../../../../../core/services/services.store";
import { dateToIso } from "../../../../../shared/date-iso";
import { parseTimeInput } from "../../../../../shared/parse-time";
import { MealsApi } from "../../../../api/meals.api";
import type {
  MealAmountDto,
  ReplaceMealRequest,
} from "../../../../api/meals.types";
import type {
  ReservationDetailMeal,
  ReservationMealAmount,
} from "../../../../api/reservations.types";
import {
  DIET_LABELS,
  DIET_TAGS,
  DIET_VARIANTS,
  type DietVariant,
  MEAL_KINDS,
  type MealAmounts,
  type MealDay,
  type MealKind,
} from "../reservation-form-stub-data";

const MEAL_SERVICE_IDS: Record<MealKind, string> = {
  breakfast: KNOWN_SERVICE_IDS.breakfast,
  lunch: KNOWN_SERVICE_IDS.lunch,
  lunchPackage: KNOWN_SERVICE_IDS.lunchPackage,
  dinner: KNOWN_SERVICE_IDS.dinner,
};

const MEAL_LABELS: Record<MealKind, string> = {
  breakfast: "Snídaně",
  lunch: "Oběd",
  lunchPackage: "Balíček",
  dinner: "Večeře",
};

const MEAL_ICONS: Record<MealKind, string> = {
  breakfast: "pi-sun",
  lunch: "pi-cloud",
  lunchPackage: "pi-shopping-bag",
  dinner: "pi-moon",
};

type DietRowView = {
  readonly variant: DietVariant;
  readonly label: string;
  readonly tag: string;
  readonly counts: Record<MealKind, number>;
  readonly subtotal: number;
  readonly removable: boolean;
};

type DayView = {
  readonly day: MealDay;
  readonly index: number;
  readonly badge: string | null;
  readonly diets: readonly DietRowView[];
  readonly totals: Record<MealKind, number>;
  readonly times: Record<MealKind, string | null>;
  readonly grandTotal: number;
  readonly addableDiets: readonly DietVariant[];
  // Stable identity across CD cycles so PrimeNG doesn't re-create the
  // overlay (which would make the toggle button need two clicks).
  readonly addDietMenuItems: MenuItem[];
  readonly dayMenuItems: MenuItem[];
};

@Component({
  selector: "kemp-is-reservation-step-meals",
  imports: [FormsModule, ButtonModule, InputNumberModule, MenuModule],
  templateUrl: "./step-meals.html",
  styleUrl: "./step-meals.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class StepMeals {
  protected readonly mealKinds = MEAL_KINDS;
  protected readonly mealLabels = MEAL_LABELS;
  protected readonly mealIcons = MEAL_ICONS;

  private readonly mealsApi = inject(MealsApi);
  private readonly servicesStore = inject(ServicesStore);

  protected readonly prices = computed<Record<MealKind, number>>(() => {
    const result = {} as Record<MealKind, number>;
    for (const kind of MEAL_KINDS) {
      result[kind] =
        this.servicesStore.byId(MEAL_SERVICE_IDS[kind])?.basePrice ?? 0;
    }
    return result;
  });

  readonly reservationId = input<string | null>(null);
  readonly reservationFrom = input<Date | null>(null);
  readonly reservationTo = input<Date | null>(null);
  readonly serverMeals = input<readonly ReservationDetailMeal[]>([]);

  readonly mutated = output<void>();

  protected readonly days = signal<readonly MealDay[]>([]);

  constructor() {
    effect(() => {
      const from = this.reservationFrom();
      const to = this.reservationTo();
      const server = this.serverMeals();
      this.days.set(buildDays(from, to, server));
    });
  }

  protected readonly dayViews = computed<DayView[]>(() => {
    const days = this.days();
    const prices = this.prices();
    return days.map((day, index) =>
      this.buildDayView(day, index, days.length, prices)
    );
  });

  protected readonly grandTotal = computed<number>(() =>
    this.dayViews().reduce((sum, dv) => sum + dv.grandTotal, 0)
  );

  // Triggered from input blur so there's no explicit save button.
  protected saveDay(idx: number): void {
    const reservationId = this.reservationId();
    if (!reservationId) {
      return;
    }
    const day = this.days()[idx];
    if (!day) {
      return;
    }
    this.mealsApi.replace(reservationId, mealDayToRequest(day)).subscribe({
      next: () => this.mutated.emit(),
      error: () => {},
    });
  }

  protected readonly totalPortions = computed<number>(() =>
    this.dayViews().reduce(
      (sum, dv) =>
        sum + MEAL_KINDS.reduce<number>((s, kind) => s + dv.totals[kind], 0),
      0
    )
  );

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }

  protected updateMeal(
    dayIdx: number,
    kind: MealKind,
    variant: DietVariant,
    value: number
  ): void {
    this.days.update(list =>
      list.map((d, i) => {
        if (i !== dayIdx) {
          return d;
        }
        const updatedKind = { ...d[kind], [variant]: Math.max(0, value) };
        const totalForKind = sumAmounts(updatedKind);
        // Clear pickup time when the kind drops to zero portions so a
        // stale time doesn't confuse the kitchen.
        const times: Record<MealKind, string | null> =
          totalForKind === 0 && d.times[kind] !== null
            ? { ...d.times, [kind]: null }
            : d.times;
        return { ...d, [kind]: updatedKind, times };
      })
    );
  }

  // Input accepts loose formats ("8" / "0800" / "8:30") and is normalized
  // to "HH:MM" by parseTimeInput. undefined means "no change".
  protected updateMealTime(
    dayIdx: number,
    kind: MealKind,
    raw: string | null | undefined
  ): void {
    const parsed = parseTimeInput(raw);
    if (parsed === undefined) {
      return;
    }
    this.days.update(list =>
      list.map((d, i) =>
        i === dayIdx
          ? {
              ...d,
              times: { ...d.times, [kind]: parsed },
            }
          : d
      )
    );
    this.saveDay(dayIdx);
  }

  protected addDietRow(dayIdx: number, variant: DietVariant): void {
    this.days.update(list =>
      list.map((d, i) => {
        if (i !== dayIdx || d.pinnedDiets.includes(variant)) {
          return d;
        }
        return { ...d, pinnedDiets: [...d.pinnedDiets, variant] };
      })
    );
    // pinnedDiets is UI-only; nothing to save.
  }

  protected removeDietRow(dayIdx: number, variant: DietVariant): void {
    if (variant === "normal") {
      return;
    }
    this.days.update(list =>
      list.map((d, i) => {
        if (i !== dayIdx) {
          return d;
        }
        const breakfast = { ...d.breakfast, [variant]: 0 };
        const lunch = { ...d.lunch, [variant]: 0 };
        const lunchPackage = { ...d.lunchPackage, [variant]: 0 };
        const dinner = { ...d.dinner, [variant]: 0 };
        const times: Record<MealKind, string | null> = {
          breakfast: sumAmounts(breakfast) === 0 ? null : d.times.breakfast,
          lunch: sumAmounts(lunch) === 0 ? null : d.times.lunch,
          lunchPackage:
            sumAmounts(lunchPackage) === 0 ? null : d.times.lunchPackage,
          dinner: sumAmounts(dinner) === 0 ? null : d.times.dinner,
        };
        return {
          ...d,
          breakfast,
          lunch,
          lunchPackage,
          dinner,
          times,
          pinnedDiets: d.pinnedDiets.filter(v => v !== variant),
        };
      })
    );
    this.saveDay(dayIdx);
  }

  private buildAddDietMenu(
    index: number,
    addableDiets: readonly DietVariant[]
  ): MenuItem[] {
    return addableDiets.map(variant => ({
      label: DIET_LABELS[variant],
      icon: "pi pi-plus",
      command: (): void => this.addDietRow(index, variant),
    }));
  }

  private buildDayMenu(index: number, totalDays: number): MenuItem[] {
    const hasNext = index < totalDays - 1;
    const hasPrev = index > 0;
    return [
      {
        label: "Kopírovat do dalšího dne",
        icon: "pi pi-arrow-right",
        disabled: !hasNext,
        command: (): void => {
          if (hasNext) {
            this.copyDay(index, index + 1);
          }
        },
      },
      {
        label: "Kopírovat z předchozího dne",
        icon: "pi pi-arrow-left",
        disabled: !hasPrev,
        command: (): void => {
          if (hasPrev) {
            this.copyDay(index - 1, index);
          }
        },
      },
      {
        label: "Kopírovat do všech dnů",
        icon: "pi pi-clone",
        disabled: totalDays < 2,
        command: (): void => this.copyDayToAll(index),
      },
      { separator: true },
      {
        label: "Vyprázdnit den",
        icon: "pi pi-eraser",
        command: (): void => this.clearDay(index),
      },
    ];
  }

  protected copyDay(fromIdx: number, toIdx: number): void {
    const list = this.days();
    const src = list[fromIdx];
    if (!src) {
      return;
    }
    this.days.update(prev =>
      prev.map((d, i) => (i === toIdx ? this.cloneDayAmounts(src, d) : d))
    );
    this.saveDay(toIdx);
  }

  protected copyDayToAll(fromIdx: number): void {
    const list = this.days();
    const src = list[fromIdx];
    if (!src) {
      return;
    }
    this.days.update(prev =>
      prev.map((d, i) => (i === fromIdx ? d : this.cloneDayAmounts(src, d)))
    );
    const total = this.days().length;
    for (let i = 0; i < total; i++) {
      if (i !== fromIdx) {
        this.saveDay(i);
      }
    }
  }

  protected clearDay(idx: number): void {
    this.days.update(prev =>
      prev.map((d, i) =>
        i !== idx
          ? d
          : {
              ...d,
              breakfast: emptyAmounts(),
              lunch: emptyAmounts(),
              lunchPackage: emptyAmounts(),
              dinner: emptyAmounts(),
              times: emptyTimes(),
              pinnedDiets: [],
            }
      )
    );
    this.saveDay(idx);
  }

  private cloneDayAmounts(src: MealDay, target: MealDay): MealDay {
    return {
      ...target,
      breakfast: { ...src.breakfast },
      lunch: { ...src.lunch },
      lunchPackage: { ...src.lunchPackage },
      dinner: { ...src.dinner },
      times: { ...src.times },
      pinnedDiets: [...src.pinnedDiets],
    };
  }

  private buildDayView(
    day: MealDay,
    index: number,
    totalDays: number,
    prices: Record<MealKind, number>
  ): DayView {
    const totals: Record<MealKind, number> = {
      breakfast: sumAmounts(day.breakfast),
      lunch: sumAmounts(day.lunch),
      lunchPackage: sumAmounts(day.lunchPackage),
      dinner: sumAmounts(day.dinner),
    };
    const grandTotal = MEAL_KINDS.reduce<number>(
      (sum, kind) => sum + totals[kind] * prices[kind],
      0
    );

    const visibleVariants = this.visibleVariants(day);
    const diets: DietRowView[] = visibleVariants.map(variant => {
      const counts: Record<MealKind, number> = {
        breakfast: day.breakfast[variant],
        lunch: day.lunch[variant],
        lunchPackage: day.lunchPackage[variant],
        dinner: day.dinner[variant],
      };
      const subtotal = MEAL_KINDS.reduce<number>(
        (sum, kind) => sum + counts[kind] * prices[kind],
        0
      );
      return {
        variant,
        label: DIET_LABELS[variant],
        tag: DIET_TAGS[variant],
        counts,
        subtotal,
        removable: variant !== "normal",
      };
    });

    const visibleSet = new Set(visibleVariants);
    const addableDiets = DIET_VARIANTS.filter(v => !visibleSet.has(v));

    const times: Record<MealKind, string | null> = {
      breakfast: day.times.breakfast,
      lunch: day.times.lunch,
      lunchPackage: day.times.lunchPackage,
      dinner: day.times.dinner,
    };

    return {
      day,
      index,
      badge: this.dayBadge(index, totalDays),
      diets,
      totals,
      times,
      grandTotal,
      addableDiets,
      addDietMenuItems: this.buildAddDietMenu(index, addableDiets),
      dayMenuItems: this.buildDayMenu(index, totalDays),
    };
  }

  // normal is always present, plus pinned variants and any with a
  // non-zero count (so old data shows up even when not pinned).
  private visibleVariants(day: MealDay): readonly DietVariant[] {
    const seen = new Set<DietVariant>(["normal"]);
    for (const v of day.pinnedDiets) {
      seen.add(v);
    }
    for (const variant of DIET_VARIANTS) {
      if (
        day.breakfast[variant] > 0 ||
        day.lunch[variant] > 0 ||
        day.lunchPackage[variant] > 0 ||
        day.dinner[variant] > 0
      ) {
        seen.add(variant);
      }
    }
    return DIET_VARIANTS.filter(v => seen.has(v));
  }

  private dayBadge(idx: number, totalDays: number): string | null {
    if (idx === 0) {
      return "Příjezd";
    }
    if (idx === totalDays - 1) {
      return "Odjezd";
    }
    return null;
  }
}

function sumAmounts(amounts: MealAmounts): number {
  return DIET_VARIANTS.reduce<number>(
    (sum, variant) => sum + amounts[variant],
    0
  );
}

function emptyAmounts(): MealAmounts {
  return {
    normal: 0,
    glutenFree: 0,
    lactoseFree: 0,
    vegetarian: 0,
    glutenFreeLactoseFree: 0,
    glutenFreeVegetarian: 0,
    lactoseFreeVegetarian: 0,
    glutenFreeLactoseFreeVegetarian: 0,
  };
}

function emptyTimes(): Record<MealKind, string | null> {
  return {
    breakfast: null,
    lunch: null,
    lunchPackage: null,
    dinner: null,
  };
}

// Drop "HH:MM:SS" to "HH:MM" so the minute-precision picker doesn't lose
// the seconds when the user re-saves.
function normalizeTimeString(value: string | null): string | null {
  if (!value) {
    return null;
  }
  const parts = value.split(":");
  if (parts.length < 2) {
    return null;
  }
  return `${parts[0]}:${parts[1]}`;
}

const CZECH_DOW = ["Ne", "Po", "Út", "St", "Čt", "Pá", "So"] as const;

function buildDays(
  from: Date | null,
  to: Date | null,
  server: readonly ReservationDetailMeal[]
): readonly MealDay[] {
  if (!from || !to) {
    return [];
  }
  const byDate = new Map<string, ReservationDetailMeal>();
  for (const m of server) {
    byDate.set(m.date, m);
  }

  const days: MealDay[] = [];
  const cursor = new Date(from);
  while (cursor.getTime() <= to.getTime()) {
    const iso = dateToIso(cursor);
    const matched = byDate.get(iso);
    days.push({
      iso,
      date: formatCzechDate(cursor),
      dow: CZECH_DOW[cursor.getDay()] ?? "",
      day: cursor.getDate(),
      breakfast: matched
        ? mealAmountFromDto(matched.breakfast)
        : emptyAmounts(),
      lunch: matched ? mealAmountFromDto(matched.lunch) : emptyAmounts(),
      lunchPackage: matched
        ? mealAmountFromDto(matched.lunchPackage)
        : emptyAmounts(),
      dinner: matched ? mealAmountFromDto(matched.dinner) : emptyAmounts(),
      times: matched
        ? {
            breakfast: normalizeTimeString(matched.breakfast.at),
            lunch: normalizeTimeString(matched.lunch.at),
            lunchPackage: normalizeTimeString(matched.lunchPackage.at),
            dinner: normalizeTimeString(matched.dinner.at),
          }
        : emptyTimes(),
      pinnedDiets: [],
    });
    cursor.setDate(cursor.getDate() + 1);
  }
  return days;
}

function formatCzechDate(d: Date): string {
  const dow = CZECH_DOW[d.getDay()] ?? "";
  return `${dow} ${d.getDate()}. ${d.getMonth() + 1}.`;
}

function mealAmountFromDto(dto: ReservationMealAmount): MealAmounts {
  return {
    normal: dto.normal,
    glutenFree: dto.glutenFree,
    lactoseFree: dto.lactoseFree,
    vegetarian: dto.vegetarian,
    glutenFreeLactoseFree: dto.glutenFreeLactoseFree,
    glutenFreeVegetarian: dto.glutenFreeVegetarian,
    lactoseFreeVegetarian: dto.lactoseFreeVegetarian,
    glutenFreeLactoseFreeVegetarian: dto.glutenFreeLactoseFreeVegetarian,
  };
}

function mealAmountToDto(
  amounts: MealAmounts,
  at: string | null
): MealAmountDto {
  return {
    at,
    ...amounts,
  };
}

function mealDayToRequest(day: MealDay): ReplaceMealRequest {
  return {
    date: day.iso,
    breakfast: mealAmountToDto(day.breakfast, day.times.breakfast),
    lunch: mealAmountToDto(day.lunch, day.times.lunch),
    lunchPackage: mealAmountToDto(day.lunchPackage, day.times.lunchPackage),
    dinner: mealAmountToDto(day.dinner, day.times.dinner),
  };
}
