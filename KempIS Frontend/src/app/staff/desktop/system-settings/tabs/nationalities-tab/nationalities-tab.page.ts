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
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { NationalityFormDialog } from "./nationality-form-dialog/nationality-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { NationalitiesApi } from "../../api/nationalities.api";
import type { Nationality } from "../../shared/types";

type TriFilter = "all" | "yes" | "no";

type FilterOption = { readonly label: string; readonly value: TriFilter };

const TRI_OPTIONS: readonly FilterOption[] = [
  { label: "Vše", value: "all" },
  { label: "Ano", value: "yes" },
  { label: "Ne", value: "no" },
];

const COLLATOR = new Intl.Collator("cs", { sensitivity: "base" });

function matchesTri(value: boolean, filter: TriFilter): boolean {
  if (filter === "all") {
    return true;
  }
  return filter === "yes" ? value : !value;
}

@Component({
  selector: "kemp-is-nationalities-tab",
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
    NationalityFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./nationalities-tab.page.html",
  styleUrl: "./nationalities-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NationalitiesTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(NationalitiesApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly Nationality[]>(() =>
    this.apiClient.url("/nationalities")
  );

  protected readonly search = signal<string>("");
  protected readonly euFilter = signal<TriFilter>("all");
  protected readonly visaFilter = signal<TriFilter>("all");
  protected readonly bioFilter = signal<TriFilter>("all");

  protected readonly triOptions: FilterOption[] = [...TRI_OPTIONS];

  protected readonly rows = computed<Nationality[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    const q = this.search().trim().toLocaleLowerCase("cs");
    const eu = this.euFilter();
    const visa = this.visaFilter();
    const bio = this.bioFilter();

    return [...this.resource.value()]
      .filter(n => {
        if (
          !matchesTri(n.isEu, eu) ||
          !matchesTri(n.visaRequired, visa) ||
          !matchesTri(n.biometricsRequired, bio)
        ) {
          return false;
        }
        if (q.length === 0) {
          return true;
        }
        return (
          n.name.toLocaleLowerCase("cs").includes(q) ||
          n.nameEn.toLocaleLowerCase("cs").includes(q) ||
          n.alpha2.toLowerCase().includes(q) ||
          n.alpha3.toLowerCase().includes(q) ||
          n.numeric.includes(q)
        );
      })
      .sort((a, b) => COLLATOR.compare(a.name, b.name));
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingNationality = signal<Nationality | null>(null);

  protected onCreate(): void {
    this.editingNationality.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(n: Nationality): void {
    this.editingNationality.set(n);
    this.formVisible.set(true);
  }

  protected onDelete(n: Nationality): void {
    this.confirm.confirm({
      header: "Smazat národnost",
      message: `Opravdu chcete smazat národnost „${n.name}"?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.delete(n.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Smazáno",
              detail: n.name,
            });
            this.resource.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Národnost se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingNationality.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }

  protected onSort(event: SortEvent): void {
    const data = event.data as Nationality[] | undefined;
    const field = event.field as keyof Nationality | undefined;
    const order = event.order ?? 1;
    if (!data || !field) {
      return;
    }
    data.sort((a, b) => {
      const av = a[field];
      const bv = b[field];
      let result: number;
      if (typeof av === "string" && typeof bv === "string") {
        result = COLLATOR.compare(av, bv);
      } else if (typeof av === "boolean" && typeof bv === "boolean") {
        result = av === bv ? 0 : av ? 1 : -1;
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
}
