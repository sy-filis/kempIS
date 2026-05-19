import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import {
  buildGroupsFromApi,
  buildReplaceRequest,
  czLongDate,
  type Group,
  isoWeekFor,
  type MealsMeta,
  sumWeekForGroup,
  type WeekDay,
  weekDaysFor,
} from "./meals-data";
import { MealsGrid } from "./meals-grid";
import { MealsToolbar, type WeekPick } from "./meals-toolbar";
import { PickupSchedule, type PickupTimeChange } from "./pickup-schedule";
import { ApiClient } from "../../../core/api/api-client";
import { MealsApi } from "../../api/meals.api";
import type { MealResponse } from "../../api/meals.types";
import type { Reservation } from "../../api/reservations.types";

@Component({
  selector: "kemp-is-meals",
  imports: [MealsToolbar, MealsGrid, PickupSchedule],
  templateUrl: "./meals.page.html",
  styleUrl: "./meals.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealsPage {
  private readonly apiClient = inject(ApiClient);
  private readonly mealsApi = inject(MealsApi);

  private readonly _today = isoWeekFor(new Date());
  protected readonly activeYear = signal<number>(this._today.year);
  protected readonly activeWeek = signal<number>(this._today.week);

  protected readonly weekDays = computed<readonly WeekDay[]>(() =>
    weekDaysFor(this.activeYear(), this.activeWeek())
  );

  protected readonly meta = computed<MealsMeta>(() => {
    const days = this.weekDays();
    const first = days[0];
    const last = days[days.length - 1];
    return {
      weekLabel: `Týden ${this.activeWeek()} · ${this.activeYear()}`,
      weekNo: this.activeWeek(),
      year: this.activeYear(),
      weekFrom: first ? czLongDate(first.iso) : "",
      weekTo: last ? czLongDate(last.iso) : "",
    };
  });

  private readonly mealsResource = httpResource<readonly MealResponse[]>(() => {
    const range = this.weekRange();
    return range
      ? `${this.apiClient.url("/meals")}?from=${range.from}&to=${range.to}`
      : undefined;
  });

  private readonly reservationsResource = httpResource<readonly Reservation[]>(
    () => {
      const range = this.weekRange();
      return range
        ? `${this.apiClient.url("/reservations")}?from=${range.from}&to=${range.to}`
        : undefined;
    }
  );

  protected readonly groups = computed<readonly Group[]>(() => {
    const meals = this.mealsResource.hasValue()
      ? this.mealsResource.value()
      : [];
    const reservations = this.reservationsResource.hasValue()
      ? this.reservationsResource.value()
      : [];
    return buildGroupsFromApi(meals, reservations, this.weekDays());
  });

  /** First-load only; subsequent reloads keep the existing table on screen to avoid flicker. */
  protected readonly isLoading = computed(
    () =>
      (this.mealsResource.isLoading() && !this.mealsResource.hasValue()) ||
      (this.reservationsResource.isLoading() &&
        !this.reservationsResource.hasValue())
  );

  /** A week with reservations but no meal plan still counts as empty. */
  protected readonly isEmpty = computed(
    () =>
      !this.isLoading() &&
      this.mealsResource.hasValue() &&
      this.groups().length === 0
  );

  protected readonly weekTotal = computed(() =>
    this.groups().reduce((acc, g) => acc + sumWeekForGroup(g), 0)
  );

  protected onWeekPick(value: WeekPick): void {
    this.activeYear.set(value.year);
    this.activeWeek.set(value.week);
  }

  protected onTimeChange(change: PickupTimeChange): void {
    const group = this.groups().find(g => g.id === change.groupId);
    if (!group) {
      return;
    }
    const day = this.weekDays().find(d => d.key === change.dayKey);
    if (!day) {
      return;
    }
    const updated = applyTimeToGroup(group, change);
    const request = buildReplaceRequest(updated, change.dayKey, day.iso);
    this.mealsApi.replace(group.id, request).subscribe({
      next: () => this.mealsResource.reload(),
      error: () => this.mealsResource.reload(),
    });
  }

  private weekRange(): { from: string; to: string } | null {
    const days = this.weekDays();
    const first = days[0];
    const last = days[days.length - 1];
    if (!first || !last) {
      return null;
    }
    return { from: first.iso, to: last.iso };
  }
}

function applyTimeToGroup(group: Group, change: PickupTimeChange): Group {
  const day = group.days[change.dayKey];
  if (!day) {
    return group;
  }
  const meal = day[change.mealKey];
  return {
    ...group,
    days: {
      ...group.days,
      [change.dayKey]: {
        ...day,
        [change.mealKey]: { ...meal, t: change.value },
      },
    },
  };
}
