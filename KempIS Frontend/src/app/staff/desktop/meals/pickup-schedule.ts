import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  type ElementRef,
  input,
  output,
  viewChild,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import {
  computeEarliest,
  type DayKey,
  type Group,
  MEAL_TYPES,
  type MealKey,
  type MealsMeta,
  sumVariants,
  type WeekDay,
} from "./meals-data";
import { parseTimeInput } from "../../../shared/parse-time";

export type PickupTimeChange = {
  readonly groupId: string;
  readonly dayKey: DayKey;
  readonly mealKey: MealKey;
  readonly value: string | null;
};

@Component({
  selector: "kemp-is-pickup-schedule",
  imports: [FormsModule],
  templateUrl: "./pickup-schedule.html",
  styleUrl: "./pickup-schedule.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PickupSchedule {
  readonly groups = input.required<readonly Group[]>();
  readonly weekDays = input.required<readonly WeekDay[]>();
  readonly meta = input.required<MealsMeta>();

  readonly timeChange = output<PickupTimeChange>();

  protected readonly mealTypes = MEAL_TYPES;

  private readonly scrollEl = viewChild<ElementRef<HTMLDivElement>>("scroll");

  constructor() {
    afterNextRender(() => this.centerOnToday());
  }

  private centerOnToday(): void {
    const host = this.scrollEl()?.nativeElement;
    if (!host) {
      return;
    }
    const today = host.querySelector<HTMLElement>(
      ".kemp-is-pickup__day-th--today"
    );
    if (!today) {
      return;
    }
    const firstDay = host.querySelector<HTMLElement>(".kemp-is-pickup__day-th");
    const frozenWidth = firstDay?.offsetLeft ?? 0;
    const visible = host.clientWidth - frozenWidth;
    const targetCenter = today.offsetLeft + today.offsetWidth / 2;
    const desired = targetCenter - frozenWidth - visible / 2;
    const max = host.scrollWidth - host.clientWidth;
    host.scrollLeft = Math.max(0, Math.min(desired, max));
  }

  protected readPortions(
    group: Group,
    dayKey: DayKey,
    mealKey: MealKey
  ): number {
    const day = group.days[dayKey];
    if (!day) {
      return 0;
    }
    return sumVariants(day[mealKey]);
  }

  protected readTime(
    group: Group,
    dayKey: DayKey,
    mealKey: MealKey
  ): string | null {
    const day = group.days[dayKey];
    if (!day) {
      return null;
    }
    return day[mealKey].t;
  }

  protected isDisabled(
    group: Group,
    dayKey: DayKey,
    mealKey: MealKey
  ): boolean {
    return this.readPortions(group, dayKey, mealKey) === 0;
  }

  protected onTimeChange(
    group: Group,
    dayKey: DayKey,
    mealKey: MealKey,
    raw: string | null | undefined
  ): void {
    // Invalid string is treated as "no change"; null means the user cleared the field.
    const parsed = parseTimeInput(raw);
    if (parsed === undefined) {
      return;
    }
    this.timeChange.emit({
      groupId: group.id,
      dayKey,
      mealKey,
      value: parsed,
    });
  }

  protected readonly earliest = computed(() => computeEarliest(this.groups()));

  protected earliestFor(
    dayKey: DayKey,
    mealKey: MealKey
  ): {
    readonly time: string | null;
  } {
    return this.earliest()[dayKey][mealKey];
  }
}
