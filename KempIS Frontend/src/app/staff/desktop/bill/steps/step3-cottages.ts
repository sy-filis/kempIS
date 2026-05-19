import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { CheckboxModule } from "primeng/checkbox";

import { BillState } from "../bill-state";

type KeyState = "out" | "returned" | "pending";

type CottageVm = {
  readonly itemId: string;
  readonly name: string;
  readonly groupName: string;
  readonly capacity: number;
  readonly nightly: number;
  readonly lineTotal: number;
  readonly selected: boolean;
  readonly billed: boolean;
  readonly keyState: KeyState;
};

@Component({
  selector: "kemp-is-bill-step3-cottages",
  imports: [FormsModule, CheckboxModule],
  templateUrl: "./step3-cottages.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step3Cottages {
  private readonly billState = inject(BillState);

  protected readonly cottages = computed<readonly CottageVm[]>(() => {
    const selected = this.billState.selectedSpotItemIds();
    const nights = Math.max(1, this.billState.nights());
    const rows = this.billState.reservationCottages().map(c => ({
      itemId: c.itemId,
      name: c.name,
      groupName: c.groupName,
      capacity: c.capacity,
      nightly: c.nightly,
      lineTotal: c.nightly * nights,
      selected: selected.has(c.itemId),
      billed: c.billId !== null,
      keyState: keyStateOf(c.hasGivenKey, c.hasReturnedKeys),
    }));
    // Natural numeric ordering so "B2" comes before "B10".
    const collator = new Intl.Collator("cs", {
      numeric: true,
      sensitivity: "base",
    });
    return [...rows].sort(
      (a, b) =>
        collator.compare(a.groupName, b.groupName) ||
        collator.compare(a.name, b.name)
    );
  });

  protected toggle(itemId: string): void {
    const cottage = this.billState
      .reservationCottages()
      .find(c => c.itemId === itemId);
    if (cottage?.billId) {
      return;
    }
    this.billState.selectedSpotItemIds.update(set => {
      const next = new Set(set);
      if (next.has(itemId)) {
        next.delete(itemId);
      } else {
        next.add(itemId);
      }
      return next;
    });
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }
}

function keyStateOf(hasGiven: boolean, hasReturned: boolean): KeyState {
  if (hasReturned) {
    return "returned";
  }
  if (hasGiven) {
    return "out";
  }
  return "pending";
}
