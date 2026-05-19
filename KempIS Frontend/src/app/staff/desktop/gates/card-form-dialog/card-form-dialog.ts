import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";
import { DialogModule } from "primeng/dialog";
import { InputNumberModule } from "primeng/inputnumber";
import { MessageModule } from "primeng/message";
import { TextareaModule } from "primeng/textarea";

import { AccessCardsApi } from "../../../../core/access-cards/access-cards.api";
import type { AccessCard } from "../../../../core/access-cards/access-cards.types";
import { dateToIso, isoToDate } from "../../../../shared/date-iso";

@Component({
  selector: "kemp-is-card-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    MessageModule,
    TextareaModule,
  ],
  templateUrl: "./card-form-dialog.html",
  styleUrl: "./card-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardFormDialog {
  private readonly api = inject(AccessCardsApi);

  readonly visible = model<boolean>(false);
  readonly card = input<AccessCard | null>(null);
  readonly saved = output<string>();

  protected readonly uid = signal<number | null>(null);
  protected readonly deposit = signal<number | null>(null);
  protected readonly note = signal<string>("");
  protected readonly validUntil = signal<Date>(this.defaultValidUntil());

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly isEdit = computed(() => this.card() !== null);

  protected readonly headerText = computed(() =>
    this.isEdit() ? "Upravit kartu" : "Nová karta"
  );

  protected readonly submitLabel = computed(() =>
    this.isEdit() ? "Uložit" : "Vytvořit"
  );

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    if (this.isEdit()) {
      return true;
    }
    const uid = this.uid();
    const deposit = this.deposit();
    return uid !== null && uid > 0 && deposit !== null && deposit >= 0;
  });

  private defaultValidUntil(): Date {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + 7);
    return d;
  }

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      this.reset();
    });
  }

  protected onVisibleChange(visible: boolean): void {
    this.visible.set(visible);
    if (!visible) {
      this.reset();
    }
  }

  protected onCancel(): void {
    if (this.submitting()) {
      return;
    }
    this.visible.set(false);
  }

  protected onSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }
    const noteRaw = this.note().trim();
    const note = noteRaw.length === 0 ? null : noteRaw;
    const validUntil = dateToIso(this.validUntil());

    this.submitting.set(true);
    this.errorMessage.set(null);

    const existing = this.card();
    if (existing) {
      this.api.update(existing.id, { validUntil, note }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Karta s UID „${existing.uid}“ byla upravena.`);
        },
        error: err => this.handleError(err),
      });
      return;
    }

    const uid = this.uid();
    const deposit = this.deposit();
    if (uid === null || deposit === null) {
      this.submitting.set(false);
      return;
    }
    this.api.issue({ uid, deposit, billId: null, note, validUntil }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.saved.emit(`Karta s UID „${uid}“ byla vytvořena.`);
      },
      error: err => this.handleError(err),
    });
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (this.isConflict(err)) {
      this.errorMessage.set("UID je už použito jinou kartou.");
      return;
    }
    this.errorMessage.set(
      "Uložení selhalo. Zkontrolujte vyplněné údaje a zkuste to znovu."
    );
  }

  private isConflict(err: unknown): boolean {
    return (
      typeof err === "object" &&
      err !== null &&
      "status" in err &&
      (err as { status: number }).status === 409
    );
  }

  private reset(): void {
    const existing = this.card();
    if (existing) {
      this.uid.set(existing.uid);
      this.deposit.set(existing.deposit);
      this.note.set(existing.note ?? "");
      this.validUntil.set(
        isoToDate(existing.validUntil) ?? this.defaultValidUntil()
      );
    } else {
      this.uid.set(null);
      this.deposit.set(null);
      this.note.set("");
      this.validUntil.set(this.defaultValidUntil());
    }
    this.errorMessage.set(null);
  }
}
