import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import type { CheckboxChangeEvent } from "primeng/checkbox";
import { CheckboxModule } from "primeng/checkbox";
import { InputTextModule } from "primeng/inputtext";

import { isoToDate } from "../../../shared/date-iso";

export type CleaningRow = {
  readonly spotId: string;
  readonly spotName: string;
  readonly groupId: string;
  readonly groupName: string;
  readonly cleanInfoId: string | null;
  readonly shouldClean: boolean;
  readonly done: boolean;
  readonly note: string;
};

type GroupBlock = {
  readonly id: string;
  readonly name: string;
  readonly rows: readonly CleaningRow[];
};

export type ShouldCleanChange = {
  readonly spotId: string;
  readonly value: boolean;
};

export type NoteCommit = {
  readonly cleanInfoId: string;
  readonly value: string;
};

@Component({
  selector: "kemp-is-ops-cleaning-table",
  imports: [FormsModule, CheckboxModule, InputTextModule],
  templateUrl: "./cleaning-table.html",
  styleUrl: "./cleaning-table.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleaningTable {
  readonly rows = input.required<readonly CleaningRow[]>();
  readonly shouldCleanCount = input.required<number>();
  readonly selectedDate = input.required<string>();
  readonly disabledShouldClean = input<ReadonlySet<string>>(new Set());

  readonly shouldCleanChange = output<ShouldCleanChange>();
  readonly noteCommit = output<NoteCommit>();

  // Holds in-flight note edits between input and blur to avoid PATCH-on-every-keystroke.
  private readonly pendingNotes = signal<ReadonlyMap<string, string>>(
    new Map()
  );

  private readonly groupBlocks = computed<readonly GroupBlock[]>(() => {
    const result: GroupBlock[] = [];
    let current: { id: string; name: string; rows: CleaningRow[] } | null =
      null;
    for (const r of this.rows()) {
      if (!current || current.id !== r.groupId) {
        current = { id: r.groupId, name: r.groupName, rows: [] };
        result.push(current);
      }
      current.rows.push(r);
    }
    return result;
  });

  protected readonly leftColumn = computed<readonly GroupBlock[]>(
    () => this.splitColumns().left
  );

  protected readonly rightColumn = computed<readonly GroupBlock[]>(
    () => this.splitColumns().right
  );

  protected readonly heading = computed(() => {
    const d = isoToDate(this.selectedDate());
    if (!d) {
      return "Plán úklidu";
    }
    const weekdayRaw = d.toLocaleDateString("cs-CZ", { weekday: "long" });
    const weekday = weekdayRaw.charAt(0).toUpperCase() + weekdayRaw.slice(1);
    return `Plán úklidu — ${weekday} ${d.getDate()}. ${d.getMonth() + 1}.`;
  });

  protected isShouldCleanDisabled(spotId: string): boolean {
    return this.disabledShouldClean().has(spotId);
  }

  protected noteValue(row: CleaningRow): string {
    if (row.cleanInfoId === null) {
      return row.note;
    }
    return this.pendingNotes().get(row.cleanInfoId) ?? row.note;
  }

  protected onShouldClean(spotId: string, event: CheckboxChangeEvent): void {
    this.shouldCleanChange.emit({ spotId, value: Boolean(event.checked) });
  }

  protected onNoteInput(row: CleaningRow, value: string): void {
    if (row.cleanInfoId === null) {
      return;
    }
    const id = row.cleanInfoId;
    this.pendingNotes.update(m => {
      const next = new Map(m);
      next.set(id, value);
      return next;
    });
  }

  protected onNoteBlur(row: CleaningRow): void {
    if (row.cleanInfoId === null) {
      return;
    }
    const id = row.cleanInfoId;
    const typed = this.pendingNotes().get(id);
    if (typed === undefined) {
      return;
    }
    this.pendingNotes.update(m => {
      const next = new Map(m);
      next.delete(id);
      return next;
    });
    if (typed === row.note) {
      return;
    }
    this.noteCommit.emit({ cleanInfoId: id, value: typed });
  }

  // Greedy split: send each next group to the column with fewer rows so heights stay roughly even.
  private splitColumns(): {
    readonly left: readonly GroupBlock[];
    readonly right: readonly GroupBlock[];
  } {
    const left: GroupBlock[] = [];
    const right: GroupBlock[] = [];
    let leftCount = 0;
    let rightCount = 0;
    for (const block of this.groupBlocks()) {
      if (leftCount <= rightCount) {
        left.push(block);
        leftCount += block.rows.length;
      } else {
        right.push(block);
        rightCount += block.rows.length;
      }
    }
    return { left, right };
  }
}
