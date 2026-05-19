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

import { UserFormDialog } from "./user-form-dialog/user-form-dialog";
import { UserPasskeysDrawer } from "./user-passkeys-drawer/user-passkeys-drawer";
import { ApiClient } from "../../../core/api/api-client";
import { Roles } from "../../../core/auth/roles";
import { UsersApi } from "../../../core/users/users.api";
import type { User } from "../../../core/users/users.types";

const ROLE_LABEL: Record<string, string> = {
  [Roles.Manager]: "Manažer",
  [Roles.Receptionist]: "Recepční",
  [Roles.Accountant]: "Účetní",
  [Roles.CleaningStaff]: "Úklid",
  [Roles.Guest]: "Host",
};

@Component({
  selector: "kemp-is-users",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    TableModule,
    TagModule,
    ToastModule,
    UserFormDialog,
    UserPasskeysDrawer,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./users.page.html",
  styleUrl: "./users.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage {
  private readonly apiClient = inject(ApiClient);
  private readonly usersApi = inject(UsersApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly users = httpResource<readonly User[]>(() =>
    this.apiClient.url("/users?includeDisabled=true")
  );

  protected readonly rows = computed<User[]>(() => {
    if (!this.users.hasValue()) {
      return [];
    }
    return [...this.users.value()].sort((a, b) =>
      a.name.localeCompare(b.name, "cs")
    );
  });

  protected readonly loading = computed(() => this.users.isLoading());

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingUser = signal<User | null>(null);

  protected readonly passkeysUserId = signal<string | null>(null);
  protected readonly passkeysVisible = computed(
    () => this.passkeysUserId() !== null
  );

  protected onCreate(): void {
    this.editingUser.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(user: User): void {
    this.editingUser.set(user);
    this.formVisible.set(true);
  }

  protected onPasskeys(user: User): void {
    this.passkeysUserId.set(user.id);
  }

  protected onDisable(user: User): void {
    this.confirm.confirm({
      header: "Deaktivovat uživatele",
      message: `Opravdu chcete deaktivovat uživatele „${user.name}“? Uživatel se nebude moci přihlásit.`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Deaktivovat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.usersApi.disable(user.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Uživatel deaktivován",
              detail: user.name,
            });
            this.users.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Uživatele se nepodařilo deaktivovat.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.editingUser.set(null);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.users.reload();
  }

  protected onPasskeysClosed(): void {
    this.passkeysUserId.set(null);
    this.users.reload();
  }

  protected primaryRole(user: User): string {
    const code = user.roles[0] ?? "";
    return ROLE_LABEL[code] ?? code;
  }

  protected roleSeverity(
    user: User
  ): "success" | "info" | "warn" | "secondary" {
    const code = user.roles[0];
    switch (code) {
      case Roles.Manager:
        return "success";
      case Roles.Receptionist:
        return "info";
      case Roles.Accountant:
        return "warn";
      default:
        return "secondary";
    }
  }

  protected formatDate(value: number): string {
    return new Date(value).toLocaleDateString("cs-CZ", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });
  }
}
