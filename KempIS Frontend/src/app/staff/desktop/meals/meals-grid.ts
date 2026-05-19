import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  type ElementRef,
  inject,
  input,
  viewChild,
} from "@angular/core";
import { Router } from "@angular/router";

import {
  computeEarliest,
  computeTotals,
  type DayKey,
  DIET_VARIANTS,
  type Group,
  MEAL_TYPES,
  type MealKey,
  type MealsMeta,
  type VariantKey,
  type WeekDay,
} from "./meals-data";

@Component({
  selector: "kemp-is-meals-grid",
  imports: [],
  templateUrl: "./meals-grid.html",
  styleUrl: "./meals-grid.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealsGrid {
  private readonly router = inject(Router);

  readonly groups = input.required<readonly Group[]>();
  readonly weekDays = input.required<readonly WeekDay[]>();
  readonly meta = input.required<MealsMeta>();

  protected readonly mealTypes = MEAL_TYPES;
  protected readonly variants = DIET_VARIANTS;

  protected onOpen(reservationId: string): void {
    void this.router.navigate([
      "/staff/auth/desktop/reservations",
      reservationId,
      "edit",
    ]);
  }

  private readonly scrollEl = viewChild<ElementRef<HTMLDivElement>>("scroll");

  constructor() {
    afterNextRender(() => this.centerOnToday());
  }

  protected readonly totals = computed(() => computeTotals(this.groups()));

  protected readonly weekGrandLabel = computed(() =>
    this.totals().weekGrand.toLocaleString("cs-CZ")
  );

  protected readonly earliest = computed(() => computeEarliest(this.groups()));

  protected earliestFor(
    dayKey: DayKey,
    mealKey: MealKey
  ): {
    readonly time: string | null;
    readonly count: number;
    readonly portions: number;
  } {
    return this.earliest()[dayKey][mealKey];
  }

  private centerOnToday(): void {
    const host = this.scrollEl()?.nativeElement;
    if (!host) {
      return;
    }
    const today = host.querySelector<HTMLElement>(
      ".kemp-is-meals-grid__day-th--today"
    );
    if (!today) {
      return;
    }
    const firstDay = host.querySelector<HTMLElement>(
      ".kemp-is-meals-grid__day-th"
    );
    const frozenWidth = firstDay?.offsetLeft ?? 0;
    const visible = host.clientWidth - frozenWidth;
    const targetCenter = today.offsetLeft + today.offsetWidth / 2;
    const desired = targetCenter - frozenWidth - visible / 2;
    const max = host.scrollWidth - host.clientWidth;
    host.scrollLeft = Math.max(0, Math.min(desired, max));
  }

  protected readVariant(
    group: Group,
    dayKey: DayKey,
    mealKey: MealKey,
    variantKey: VariantKey
  ): number {
    const day = group.days[dayKey];
    if (!day) {
      return 0;
    }
    return day[mealKey][variantKey];
  }

  protected variantTotal(
    dayKey: DayKey,
    mealKey: MealKey,
    variantKey: VariantKey
  ): number {
    return this.totals().totals[dayKey][mealKey][variantKey];
  }

  protected variantTint(color: string): string {
    return `${color}14`;
  }
}
