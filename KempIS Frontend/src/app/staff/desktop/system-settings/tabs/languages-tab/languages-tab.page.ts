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
import { ToastModule } from "primeng/toast";

import { LanguageFormDialog } from "./language-form-dialog/language-form-dialog";
import { ApiClient } from "../../../../../core/api/api-client";
import { isApiError } from "../../../../../core/api/api-error";
import { LanguagesApi } from "../../api/languages.api";
import type { Language } from "../../shared/types";

@Component({
  selector: "kemp-is-languages-tab",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    TableModule,
    ToastModule,
    LanguageFormDialog,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./languages-tab.page.html",
  styleUrl: "./languages-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguagesTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(LanguagesApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly resource = httpResource<readonly Language[]>(() =>
    this.apiClient.url("/languages")
  );

  protected readonly rows = computed<Language[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    return [...this.resource.value()].sort((a, b) =>
      a.code.localeCompare(b.code, "cs")
    );
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingLanguage = signal<Language | null>(null);

  protected onCreate(): void {
    this.editingLanguage.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(language: Language): void {
    this.editingLanguage.set(language);
    this.formVisible.set(true);
  }

  protected onDelete(language: Language): void {
    this.confirm.confirm({
      header: "Smazat jazyk",
      message: `Opravdu chcete smazat jazyk „${language.name}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.delete(language.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Smazáno",
              detail: language.name,
            });
            this.resource.reload();
          },
          error: err => {
            if (isApiError(err) && err.status === 409) {
              this.messages.add({
                severity: "warn",
                summary: "Nelze smazat",
                detail: "Jazyk nelze smazat, používá ho národnost.",
              });
              return;
            }
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Jazyk se nepodařilo smazat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingLanguage.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.resource.reload();
  }
}
