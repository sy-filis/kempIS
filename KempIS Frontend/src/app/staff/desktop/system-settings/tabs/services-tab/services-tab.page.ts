import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { ServiceFormDialog } from "./service-form-dialog/service-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { ServicesApi } from "../../api/services.api";
import type { ServiceGroup } from "../../shared/service-groups";
import {
  SERVICE_GROUP_LABELS,
  SERVICE_GROUP_OPTIONS,
} from "../../shared/service-groups";
import type { Service, ServiceType, VatRate } from "../../shared/types";

const PRICE_FORMATTER = new Intl.NumberFormat("cs-CZ", {
  style: "currency",
  currency: "CZK",
  maximumFractionDigits: 2,
});

type ServiceRow = Service & {
  readonly groupLabel: string;
  readonly typeName: string;
  readonly vatName: string;
};

type ServiceGroupFilterOption = {
  readonly label: string;
  readonly value: ServiceGroup | null;
};

const ALL_SERVICE_GROUPS: ServiceGroupFilterOption = {
  label: "Vše",
  value: null,
};

const SERVICE_GROUP_FILTER_OPTIONS: readonly ServiceGroupFilterOption[] = [
  ALL_SERVICE_GROUPS,
  ...SERVICE_GROUP_OPTIONS.map(o => ({ label: o.label, value: o.value })),
];

@Component({
  selector: "kemp-is-services-tab",
  imports: [
    FormsModule,
    ButtonModule,
    ConfirmDialogModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    ToastModule,
    ServiceFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./services-tab.page.html",
  styleUrl: "./services-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServicesTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(ServicesApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly Service[]>(() =>
    this.apiClient.url("/services")
  );
  protected readonly serviceTypesResource = httpResource<
    readonly ServiceType[]
  >(() => this.apiClient.url("/service-types"));
  protected readonly vatRatesResource = httpResource<readonly VatRate[]>(() =>
    this.apiClient.url("/vat-rates")
  );

  protected readonly serviceGroupFilter = signal<ServiceGroup | null>(null);
  protected readonly serviceGroupFilterOptions: ServiceGroupFilterOption[] = [
    ...SERVICE_GROUP_FILTER_OPTIONS,
  ];

  private readonly serviceTypeIndex = computed<Map<string, ServiceType>>(
    () =>
      new Map(
        this.serviceTypesResource.hasValue()
          ? this.serviceTypesResource.value().map(t => [t.id, t])
          : []
      )
  );

  private readonly vatRateIndex = computed<Map<string, VatRate>>(
    () =>
      new Map(
        this.vatRatesResource.hasValue()
          ? this.vatRatesResource.value().map(v => [v.id, v])
          : []
      )
  );

  protected readonly rows = computed<ServiceRow[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    const types = this.serviceTypeIndex();
    const vats = this.vatRateIndex();
    const filter = this.serviceGroupFilter();
    const enriched = this.resource.value().map<ServiceRow>(s => ({
      ...s,
      groupLabel: SERVICE_GROUP_LABELS[s.serviceGroup],
      typeName: types.get(s.serviceTypeId)?.name ?? "—",
      vatName: vats.get(s.vatRateId)?.name ?? "—",
    }));
    const filtered =
      filter === null
        ? enriched
        : enriched.filter(s => s.serviceGroup === filter);
    return filtered.sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingService = signal<Service | null>(null);

  protected onCreate(): void {
    this.editingService.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(s: ServiceRow): void {
    this.editingService.set(s);
    this.formVisible.set(true);
  }

  protected onDelete(s: ServiceRow): void {
    this.confirm.confirm({
      header: "Smazat službu",
      message: `Opravdu chcete smazat službu „${s.name}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.delete(s.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Smazáno",
              detail: s.name,
            });
            this.resource.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Službu se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingService.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }

  protected formatPrice(price: number): string {
    return PRICE_FORMATTER.format(price);
  }
}
