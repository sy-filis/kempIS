import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";

import { dateToIso, isoToDate } from "../../../shared/date-iso";

type DayCell = {
  readonly key: string;
  readonly dow: string;
  readonly day: number;
  readonly today: boolean;
};

const DOW_CZ: readonly string[] = ["Ne", "Po", "Út", "St", "Čt", "Pá", "So"];

@Component({
  selector: "kemp-is-ops-cleaning-toolbar",
  imports: [FormsModule, ButtonModule, DatePickerModule],
  templateUrl: "./cleaning-toolbar.html",
  styleUrl: "./cleaning-toolbar.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleaningToolbar {
  readonly selectedDate = input.required<string>();
  readonly done = input.required<number>();
  readonly total = input.required<number>();
  readonly lastUpdatedLabel = input<string | null>(null);
  readonly prefilling = input<boolean>(false);

  readonly dateChange = output<string>();
  readonly prefill = output<void>();

  protected readonly days = computed<readonly DayCell[]>(() => {
    const center = isoToDate(this.selectedDate()) ?? new Date();
    const todayIso = dateToIso(new Date());
    const cells: DayCell[] = [];
    for (let offset = -3; offset <= 3; offset += 1) {
      const d = new Date(
        center.getFullYear(),
        center.getMonth(),
        center.getDate() + offset
      );
      const iso = dateToIso(d);
      cells.push({
        key: iso,
        dow: DOW_CZ[d.getDay()] ?? "",
        day: d.getDate(),
        today: iso === todayIso,
      });
    }
    return cells;
  });

  protected readonly progressPct = computed(() => {
    const t = this.total();
    return t === 0 ? 0 : (this.done() / t) * 100;
  });

  protected readonly pickerDate = computed(() =>
    isoToDate(this.selectedDate())
  );

  protected pick(iso: string): void {
    this.dateChange.emit(iso);
  }

  protected step(days: number): void {
    const current = isoToDate(this.selectedDate()) ?? new Date();
    const next = new Date(
      current.getFullYear(),
      current.getMonth(),
      current.getDate() + days
    );
    this.dateChange.emit(dateToIso(next));
  }

  protected onPickerSelect(value: Date | null): void {
    if (!value) {
      return;
    }
    this.dateChange.emit(dateToIso(value));
  }

  protected onPrefill(): void {
    this.prefill.emit();
  }

  protected isActive(key: string): boolean {
    return key === this.selectedDate();
  }
}
