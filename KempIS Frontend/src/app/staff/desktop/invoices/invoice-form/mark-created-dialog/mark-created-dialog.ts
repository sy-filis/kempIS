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
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";

import { isApiError } from "../../../../../core/api/api-error";
import { InvoicesApi } from "../../../../api/invoices.api";

const NUMBER_MAX_LENGTH = 50;

/** Local-calendar YYYY-MM-DD (the picker stays in local time). */
function toIsoDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function addWorkDays(start: Date, workDays: number): Date {
  const d = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  let added = 0;
  while (added < workDays) {
    d.setDate(d.getDate() + 1);
    const dow = d.getDay();
    if (dow !== 0 && dow !== 6) {
      added++;
    }
  }
  return d;
}

const DEFAULT_DUE_WORK_DAYS = 14;

@Component({
  selector: "kemp-is-mark-created-dialog",
  standalone: true,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    MessageModule,
  ],
  templateUrl: "./mark-created-dialog.html",
  styleUrl: "./mark-created-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MarkCreatedDialog {
  private readonly invoicesApi = inject(InvoicesApi);

  readonly invoiceId = input.required<string>();
  readonly visible = model<boolean>(false);
  readonly completed = output<void>();

  protected readonly numberMaxLength = NUMBER_MAX_LENGTH;

  protected readonly number = signal<string>("");
  protected readonly issuedAt = signal<Date | null>(new Date());
  protected readonly dueAt = signal<Date | null>(
    addWorkDays(new Date(), DEFAULT_DUE_WORK_DAYS)
  );

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.number.set("");
        this.issuedAt.set(new Date());
        this.dueAt.set(addWorkDays(new Date(), DEFAULT_DUE_WORK_DAYS));
        this.errorMessage.set(null);
      }
    });
  }

  protected readonly canSubmit = computed<boolean>(() => {
    if (this.submitting()) {
      return false;
    }
    const trimmed = this.number().trim();
    if (trimmed.length === 0 || trimmed.length > NUMBER_MAX_LENGTH) {
      return false;
    }
    return this.issuedAt() !== null && this.dueAt() !== null;
  });

  protected onVisibleChange(value: boolean): void {
    this.visible.set(value);
    if (!value) {
      this.errorMessage.set(null);
    }
  }

  protected onSubmit(): void {
    const issued = this.issuedAt();
    const due = this.dueAt();
    if (!issued || !due) {
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.invoicesApi
      .markCreated(this.invoiceId(), {
        number: this.number().trim(),
        issuedAt: toIsoDate(issued),
        dueTo: toIsoDate(due),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.visible.set(false);
          this.completed.emit();
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.errorMessage.set(toCzechError(err));
        },
      });
  }

  protected onCancel(): void {
    if (this.submitting()) {
      return;
    }
    this.visible.set(false);
  }
}

function toCzechError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 409) {
      return "Faktura už byla vystavena nebo je číslo obsazené.";
    }
    if (err.status === 400) {
      return err.detail;
    }
  }
  return "Vystavení faktury se nezdařilo.";
}
