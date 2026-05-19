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

import { ServiceTypeFormDialog } from "./service-type-form-dialog/service-type-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { ServiceTypesApi } from "../../api/service-types.api";
import type { ServiceType } from "../../shared/types";

@Component({
  selector: "kemp-is-service-types-tab",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    TableModule,
    TagModule,
    ToastModule,
    ServiceTypeFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./service-types-tab.page.html",
  styleUrl: "./service-types-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceTypesTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(ServiceTypesApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly ServiceType[]>(() =>
    this.apiClient.url("/service-types")
  );

  protected readonly rows = computed<ServiceType[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    return [...this.resource.value()].sort((a, b) =>
      a.name.localeCompare(b.name, "cs")
    );
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingServiceType = signal<ServiceType | null>(null);

  protected onCreate(): void {
    this.editingServiceType.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(t: ServiceType): void {
    this.editingServiceType.set(t);
    this.formVisible.set(true);
  }

  protected onDelete(t: ServiceType): void {
    this.confirm.confirm({
      header: "Smazat typ služby",
      message: `Opravdu chcete smazat typ „${t.name}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.delete(t.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Smazáno",
              detail: t.name,
            });
            this.resource.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Typ se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingServiceType.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }
}
