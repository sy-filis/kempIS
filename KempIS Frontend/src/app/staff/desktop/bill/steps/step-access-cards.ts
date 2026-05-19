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
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";

import { dateToIso, isoToDate } from "../../../../shared/date-iso";
import { type AccessCard, BillState } from "../bill-state";

const DEFAULT_DEPOSIT = 500;

function defaultValidUntil(checkout: Date | null): Date {
  if (checkout) {
    const d = new Date(checkout);
    d.setHours(0, 0, 0, 0);
    return d;
  }
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  d.setDate(d.getDate() + 7);
  return d;
}

@Component({
  selector: "kemp-is-bill-step-access-cards",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    InputNumberModule,
    InputTextModule,
  ],
  templateUrl: "./step-access-cards.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StepAccessCards {
  private readonly billState = inject(BillState);

  protected readonly cards = this.billState.accessCards;
  protected readonly draftUid = signal<string>("");
  protected readonly draftDeposit = signal<number | null>(DEFAULT_DEPOSIT);
  protected readonly draftValidUntil = signal<Date>(
    defaultValidUntil(this.billState.to())
  );
  protected readonly draftNote = signal<string>("");

  protected readonly draftValid = computed(
    () => this.draftUid().trim().length > 0 && (this.draftDeposit() ?? 0) >= 0
  );

  protected readonly depositTotal = computed(() =>
    this.cards().reduce((s, c) => s + c.deposit, 0)
  );

  protected addCard(): void {
    if (!this.draftValid()) {
      return;
    }
    const card: AccessCard = {
      id: crypto.randomUUID(),
      uid: this.draftUid().trim(),
      deposit: Math.max(0, this.draftDeposit() ?? 0),
      validUntil: dateToIso(this.draftValidUntil()),
      note: this.draftNote().trim(),
    };
    this.cards.update(list => [...list, card]);
    this.draftUid.set("");
    this.draftDeposit.set(DEFAULT_DEPOSIT);
    this.draftValidUntil.set(defaultValidUntil(this.billState.to()));
    this.draftNote.set("");
  }

  protected removeCard(id: string): void {
    this.cards.update(list => list.filter(c => c.id !== id));
  }

  protected updateUid(id: string, uid: string): void {
    this.cards.update(list => list.map(c => (c.id === id ? { ...c, uid } : c)));
  }

  protected updateDeposit(id: string, deposit: number | null): void {
    this.cards.update(list =>
      list.map(c =>
        c.id === id ? { ...c, deposit: Math.max(0, deposit ?? 0) } : c
      )
    );
  }

  protected updateValidUntil(id: string, date: Date | null): void {
    if (!date) {
      return;
    }
    const iso = dateToIso(date);
    this.cards.update(list =>
      list.map(c => (c.id === id ? { ...c, validUntil: iso } : c))
    );
  }

  protected updateNote(id: string, note: string): void {
    this.cards.update(list =>
      list.map(c => (c.id === id ? { ...c, note } : c))
    );
  }

  protected validUntilDate(iso: string): Date | null {
    return isoToDate(iso);
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }
}
