import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { PrimeTemplate } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { MultiSelectModule } from "primeng/multiselect";
import { TableModule } from "primeng/table";

export type ReservationRowKind =
  | "paid"
  | "group"
  | "linkedToGroup"
  | "confirmed"
  | "cancelled";

export type ReservationRow = {
  readonly id: string;
  readonly number: string;
  readonly name: string;
  readonly surname: string;
  readonly displayName: string;
  readonly phone: string;
  readonly fromIso: string;
  readonly toIso: string;
  readonly fromLabel: string;
  readonly toLabel: string;
  readonly nights: number;
  readonly cottage: string;
  readonly stateLabel: string;
  readonly stateKind: ReservationRowKind;
  readonly isGroup: boolean;
};

type StateBadgeStyle = {
  readonly bg: string;
  readonly fg: string;
  readonly dot: string;
};

const STATE_BADGE_STYLES: Record<ReservationRowKind, StateBadgeStyle> = {
  paid: { bg: "#fce7f3", fg: "#9d174d", dot: "#ec4899" },
  group: { bg: "#fef3c7", fg: "#78350f", dot: "#f59e0b" },
  linkedToGroup: { bg: "#ccfbf1", fg: "#134e4a", dot: "#0d9488" },
  confirmed: { bg: "#eef2ff", fg: "#312e81", dot: "#6366f1" },
  cancelled: { bg: "#f4f4f5", fg: "#52525b", dot: "#a1a1aa" },
};

type StateFilterOption = {
  readonly label: string;
  readonly value: string;
};

@Component({
  selector: "kemp-is-reservations-table",
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    MultiSelectModule,
    PrimeTemplate,
  ],
  templateUrl: "./reservations-table.html",
  styleUrl: "./reservations-table.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservationsTable {
  readonly rows = input.required<readonly ReservationRow[]>();
  readonly loading = input<boolean>(false);
  readonly emptyMessage = input<string>("Žádné rezervace");
  readonly totalLabel = input<string>("rezervací");
  readonly showStateColumn = input<boolean>(true);

  readonly rowClicked = output<string>();

  protected readonly selectedStates = signal<readonly string[]>([]);

  protected readonly stateOptions = computed<StateFilterOption[]>(() => {
    const counts = new Map<string, number>();
    for (const r of this.rows()) {
      counts.set(r.stateLabel, (counts.get(r.stateLabel) ?? 0) + 1);
    }
    return [...counts.entries()]
      .sort((a, b) => a[0].localeCompare(b[0], "cs"))
      .map(([label, count]) => ({
        label: `${label} (${count})`,
        value: label,
      }));
  });

  protected readonly displayedRows = computed<ReservationRow[]>(() => {
    const all = [...this.rows()];
    const sel = this.selectedStates();
    if (sel.length === 0) {
      return all;
    }
    const set = new Set(sel);
    return all.filter(r => set.has(r.stateLabel));
  });

  protected readonly summary = computed(() => {
    const total = this.rows().length;
    const shown = this.displayedRows().length;
    if (shown === total) {
      return `Zobrazeno ${total} ${this.totalLabel()}`;
    }
    return `Zobrazeno ${shown} z ${total} ${this.totalLabel()}`;
  });

  protected onStateFilterChange(value: readonly string[]): void {
    this.selectedStates.set(value);
  }

  protected styleFor(kind: ReservationRowKind): StateBadgeStyle {
    return STATE_BADGE_STYLES[kind];
  }

  protected onRowClick(row: ReservationRow): void {
    this.rowClicked.emit(row.id);
  }
}
