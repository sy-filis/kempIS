import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { VatRateFormDialog } from "./vat-rate-form-dialog/vat-rate-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { VatRatesApi } from "../../api/vat-rates.api";
import type { VatRate } from "../../shared/types";

@Component({
  selector: "kemp-is-vat-rates-tab",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    TableModule,
    TagModule,
    ToastModule,
    VatRateFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./vat-rates-tab.page.html",
  styleUrl: "./vat-rates-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VatRatesTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(VatRatesApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly VatRate[]>(() =>
    this.apiClient.url("/vat-rates")
  );

  protected readonly rows = computed<VatRate[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    return [...this.resource.value()].sort(
      (a, b) => a.rate - b.rate || a.name.localeCompare(b.name, "cs")
    );
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingVatRate = signal<VatRate | null>(null);

  protected onCreate(): void {
    this.editingVatRate.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(rate: VatRate): void {
    this.editingVatRate.set(rate);
    this.formVisible.set(true);
  }

  protected onDelete(rate: VatRate): void {
    this.confirm.confirm({
      header: "Smazat sazbu",
      message: `Opravdu chcete smazat sazbu „${rate.name}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.delete(rate.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Smazáno",
              detail: rate.name,
            });
            this.resource.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Sazbu se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingVatRate.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }

  protected formatRate(rate: number): string {
    return `${rate.toLocaleString("cs-CZ", {
      maximumFractionDigits: 2,
    })} %`;
  }
}
