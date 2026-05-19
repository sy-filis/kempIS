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
import { DialogModule } from "primeng/dialog";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { ToggleSwitchModule } from "primeng/toggleswitch";

import { isApiError } from "../../../../../../core/api/api-error";
import { VatRatesApi } from "../../../api/vat-rates.api";
import type { VatRate } from "../../../shared/types";

@Component({
  selector: "kemp-is-vat-rate-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    ToggleSwitchModule,
  ],
  templateUrl: "./vat-rate-form-dialog.html",
  styleUrl: "./vat-rate-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VatRateFormDialog {
  private readonly api = inject(VatRatesApi);

  readonly visible = model<boolean>(false);
  readonly vatRate = input<VatRate | null>(null);

  readonly saved = output<string>();

  protected readonly title = computed(() =>
    this.vatRate() ? "Upravit sazbu DPH" : "Nová sazba DPH"
  );

  protected readonly name = signal<string>("");
  protected readonly rate = signal<number | null>(null);
  protected readonly isActive = signal<boolean>(true);
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    const r = this.rate();
    return (
      this.name().trim().length > 0 &&
      this.name().trim().length <= 100 &&
      r !== null &&
      r >= 0 &&
      r <= 100
    );
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const v = this.vatRate();
      if (v) {
        this.name.set(v.name);
        this.rate.set(v.rate);
        this.isActive.set(v.isActive);
      } else {
        this.reset();
      }
      this.errorMessage.set(null);
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
    const existing = this.vatRate();
    const name = this.name().trim();
    const rate = this.rate() ?? 0;
    const isActive = this.isActive();

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, { name, rate, isActive }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Sazba „${name}“ byla uložena.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create({ name, rate, isActive }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Sazba „${name}“ byla vytvořena.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (isApiError(err) && err.status === 404) {
      this.errorMessage.set("Záznam již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Zkontrolujte vyplněné údaje.");
  }

  private reset(): void {
    this.name.set("");
    this.rate.set(null);
    this.isActive.set(true);
    this.errorMessage.set(null);
  }
}
