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

import { SpotGroupFormDialog } from "./spot-group-form-dialog/spot-group-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { SpotGroupsApi } from "../../api/spot-groups.api";
import type { CatalogueSpotGroup, Service } from "../../shared/types";

type SpotGroupRow = CatalogueSpotGroup & { readonly serviceName: string };

@Component({
  selector: "kemp-is-spot-groups-tab",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    TableModule,
    TagModule,
    ToastModule,
    SpotGroupFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./spot-groups-tab.page.html",
  styleUrl: "./spot-groups-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpotGroupsTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(SpotGroupsApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly CatalogueSpotGroup[]>(
    () => this.apiClient.url("/spot-groups")
  );

  protected readonly servicesResource = httpResource<readonly Service[]>(() =>
    this.apiClient.url("/services")
  );

  private readonly serviceIndex = computed<Map<string, Service>>(
    () =>
      new Map(
        this.servicesResource.hasValue()
          ? this.servicesResource.value().map(s => [s.id, s])
          : []
      )
  );

  protected readonly rows = computed<SpotGroupRow[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    const index = this.serviceIndex();
    return this.resource
      .value()
      .map(sg => ({ ...sg, serviceName: index.get(sg.serviceId)?.name ?? "—" }))
      .sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingSpotGroup = signal<CatalogueSpotGroup | null>(null);

  protected onCreate(): void {
    this.editingSpotGroup.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(sg: SpotGroupRow): void {
    this.editingSpotGroup.set(sg);
    this.formVisible.set(true);
  }

  protected onDelete(sg: SpotGroupRow): void {
    this.confirm.confirm({
      header: "Smazat skupinu míst",
      message: `Opravdu chcete smazat skupinu „${sg.name}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.delete(sg.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Smazáno",
              detail: sg.name,
            });
            this.resource.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Skupinu se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingSpotGroup.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }
}
