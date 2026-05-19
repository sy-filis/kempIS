import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import type { SortEvent } from "primeng/api";
import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { SpotFormDialog } from "./spot-form-dialog/spot-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { SpotsApi } from "../../api/spots.api";
import type { CatalogueSpot, CatalogueSpotGroup } from "../../shared/types";

type GroupFilterOption = {
  readonly id: string | null;
  readonly name: string;
};

const ALL_GROUPS: GroupFilterOption = { id: null, name: "Vše" };

type SpotRow = CatalogueSpot & { readonly groupName: string };

const NATURAL_COLLATOR = new Intl.Collator("cs", {
  numeric: true,
  sensitivity: "base",
});

@Component({
  selector: "kemp-is-spots-tab",
  imports: [
    FormsModule,
    ButtonModule,
    ConfirmDialogModule,
    SelectModule,
    TableModule,
    TagModule,
    ToastModule,
    SpotFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./spots-tab.page.html",
  styleUrl: "./spots-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpotsTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(SpotsApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly CatalogueSpot[]>(() =>
    this.apiClient.url("/spots")
  );
  protected readonly spotGroupsResource = httpResource<
    readonly CatalogueSpotGroup[]
  >(() => this.apiClient.url("/spot-groups"));

  protected readonly groupFilter = signal<string | null>(null);

  protected readonly groupFilterOptions = computed<GroupFilterOption[]>(() => {
    const groups = this.spotGroupsResource.hasValue()
      ? [...this.spotGroupsResource.value()].sort((a, b) =>
          a.name.localeCompare(b.name, "cs")
        )
      : [];
    return [ALL_GROUPS, ...groups.map(g => ({ id: g.id, name: g.name }))];
  });

  private readonly groupIndex = computed<Map<string, CatalogueSpotGroup>>(
    () =>
      new Map(
        this.spotGroupsResource.hasValue()
          ? this.spotGroupsResource.value().map(g => [g.id, g])
          : []
      )
  );

  protected readonly rows = computed<SpotRow[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    const filter = this.groupFilter();
    const index = this.groupIndex();
    const enriched = this.resource.value().map(s => ({
      ...s,
      groupName: index.get(s.spotGroupId)?.name ?? "—",
    }));
    const filtered =
      filter === null
        ? enriched
        : enriched.filter(s => s.spotGroupId === filter);
    return filtered.sort((a, b) => {
      const byGroup = NATURAL_COLLATOR.compare(a.groupName, b.groupName);
      return byGroup !== 0 ? byGroup : NATURAL_COLLATOR.compare(a.name, b.name);
    });
  });

  protected onSort(event: SortEvent): void {
    const data = event.data as SpotRow[] | undefined;
    const field = event.field as keyof SpotRow | undefined;
    const order = event.order ?? 1;
    if (!data || !field) {
      return;
    }
    data.sort((a, b) => {
      const av = a[field];
      const bv = b[field];
      const aNull = av === null;
      const bNull = bv === null;
      let result: number;
      if (aNull && bNull) {
        result = 0;
      } else if (aNull) {
        result = -1;
      } else if (bNull) {
        result = 1;
      } else if (typeof av === "string" && typeof bv === "string") {
        result = NATURAL_COLLATOR.compare(av, bv);
      } else if (av < bv) {
        result = -1;
      } else if (av > bv) {
        result = 1;
      } else {
        result = 0;
      }
      return order * result;
    });
  }

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingSpot = signal<CatalogueSpot | null>(null);
  protected readonly defaultGroupForCreate = computed<string | null>(() =>
    this.groupFilter()
  );

  protected onCreate(): void {
    this.editingSpot.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(s: SpotRow): void {
    this.editingSpot.set(s);
    this.formVisible.set(true);
  }

  protected onDelete(s: SpotRow): void {
    this.confirm.confirm({
      header: "Smazat místo",
      message: `Opravdu chcete smazat místo „${s.name}“?`,
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
              detail: "Místo se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingSpot.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }
}
