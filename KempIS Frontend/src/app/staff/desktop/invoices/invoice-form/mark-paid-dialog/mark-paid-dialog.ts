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
import { MessageModule } from "primeng/message";

import { isApiError } from "../../../../../core/api/api-error";
import { InvoicesApi } from "../../../../api/invoices.api";

/** Local-calendar YYYY-MM-DD (the picker stays in local time). */
function toIsoDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function todayAtMidnight(): Date {
  const n = new Date();
  return new Date(n.getFullYear(), n.getMonth(), n.getDate());
}

@Component({
  selector: "kemp-is-mark-paid-dialog",
  standalone: true,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    MessageModule,
  ],
  templateUrl: "./mark-paid-dialog.html",
  styleUrl: "./mark-paid-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MarkPaidDialog {
  private readonly invoicesApi = inject(InvoicesApi);

  readonly invoiceId = input.required<string>();
  readonly issuedAt = input.required<string>();
  readonly visible = model<boolean>(false);
  readonly completed = output<void>();

  protected readonly paidAt = signal<Date | null>(todayAtMidnight());
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly paidMaxDate = todayAtMidnight();

  protected readonly paidMinDate = computed<Date | null>(() => {
    const iso = this.issuedAt();
    const parts = iso.split("-");
    if (parts.length !== 3) {
      return null;
    }
    const [y, m, d] = parts.map(Number) as [number, number, number];
    if ([y, m, d].some(n => Number.isNaN(n))) {
      return null;
    }
    return new Date(y, m - 1, d);
  });

  protected readonly canSubmit = computed<boolean>(() => {
    if (this.submitting()) {
      return false;
    }
    const paid = this.paidAt();
    if (paid === null) {
      return false;
    }
    const min = this.paidMinDate();
    if (min !== null && paid < min) {
      return false;
    }
    return true;
  });

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.paidAt.set(todayAtMidnight());
        this.errorMessage.set(null);
      }
    });
  }

  protected onVisibleChange(value: boolean): void {
    this.visible.set(value);
    if (!value) {
      this.errorMessage.set(null);
    }
  }

  protected onSubmit(): void {
    const paid = this.paidAt();
    if (!paid) {
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.invoicesApi
      .markPaid(this.invoiceId(), {
        paidAt: toIsoDate(paid),
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
  if (isApiError(err) && err.status === 409) {
    return "Faktura není ve stavu Vystaveno nebo je již zaplacená.";
  }
  return "Označení faktury jako zaplacené se nezdařilo.";
}
