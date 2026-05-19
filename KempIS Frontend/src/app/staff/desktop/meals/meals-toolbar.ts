import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from "@angular/core";

import { ButtonModule } from "primeng/button";

import {
  addWeeks,
  isoWeekFor,
  isoWeekStart,
  type WeekPickerEntry,
} from "./meals-data";

export type WeekPick = {
  readonly year: number;
  readonly week: number;
};

const VISIBLE_WEEKS = [-2, -1, 0, 1, 2] as const;

@Component({
  selector: "kemp-is-meals-toolbar",
  imports: [ButtonModule],
  templateUrl: "./meals-toolbar.html",
  styleUrl: "./meals-toolbar.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MealsToolbar {
  readonly activeYear = input.required<number>();
  readonly activeWeek = input.required<number>();
  readonly groupCount = input.required<number>();
  readonly weekTotal = input.required<number>();

  readonly weekPick = output<WeekPick>();

  protected readonly weeks = computed<readonly WeekPickerEntry[]>(() => {
    const today = isoWeekFor(new Date());
    return VISIBLE_WEEKS.map(delta => {
      const { year, week } = addWeeks(
        this.activeYear(),
        this.activeWeek(),
        delta
      );
      const start = isoWeekStart(year, week);
      const end = new Date(start);
      end.setUTCDate(start.getUTCDate() + 6);
      return {
        year,
        week,
        label: `Týden ${week}`,
        range: formatRange(start, end),
        current: year === today.year && week === today.week,
      };
    });
  });

  protected formatTotal(value: number): string {
    return value.toLocaleString("cs-CZ");
  }

  protected onPick(entry: WeekPickerEntry): void {
    this.weekPick.emit({ year: entry.year, week: entry.week });
  }

  protected step(direction: number): void {
    const next = addWeeks(this.activeYear(), this.activeWeek(), direction);
    this.weekPick.emit(next);
  }
}

function formatRange(start: Date, end: Date): string {
  const sd = start.getUTCDate();
  const sm = start.getUTCMonth() + 1;
  const ed = end.getUTCDate();
  const em = end.getUTCMonth() + 1;
  return sm === em ? `${sd}.–${ed}. ${em}.` : `${sd}. ${sm}.–${ed}. ${em}.`;
}
