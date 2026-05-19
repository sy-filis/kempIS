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
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";

import { Roles } from "../../../../core/auth/roles";
import { UsersApi } from "../../../../core/users/users.api";
import type { User } from "../../../../core/users/users.types";

type RoleOption = {
  readonly label: string;
  readonly value: string;
};

const ROLE_OPTIONS: RoleOption[] = [
  { label: "Manažer", value: Roles.Manager },
  { label: "Recepční", value: Roles.Receptionist },
  { label: "Účetní", value: Roles.Accountant },
  { label: "Úklid", value: Roles.CleaningStaff },
];

@Component({
  selector: "kemp-is-user-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    SelectModule,
  ],
  templateUrl: "./user-form-dialog.html",
  styleUrl: "./user-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserFormDialog {
  private readonly usersApi = inject(UsersApi);

  readonly visible = model<boolean>(false);
  readonly user = input<User | null>(null);

  readonly saved = output<string>();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.user() ? "edit" : "create"
  );

  protected readonly title = computed(() =>
    this.mode() === "edit" ? "Upravit uživatele" : "Nový uživatel"
  );

  protected readonly name = signal<string>("");
  protected readonly username = signal<string>("");
  protected readonly role = signal<string>(Roles.Receptionist);
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly roleOptions = ROLE_OPTIONS;

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    return (
      this.name().trim().length > 0 &&
      this.username().trim().length > 0 &&
      this.role().length > 0
    );
  });

  constructor() {
    // Reopening the same row keeps `user` reference-equal, so track `visible` to retrigger the prefill.
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const u = this.user();
      if (u) {
        this.name.set(u.name);
        this.username.set(u.username);
        this.role.set(u.roles[0] ?? Roles.Receptionist);
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
    const existing = this.user();
    const name = this.name().trim();
    const username = this.username().trim();
    const role = this.role();

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.usersApi
        .update(existing.id, { name, username, roles: [role] })
        .subscribe({
          next: () => {
            this.submitting.set(false);
            this.saved.emit(`Uživatel „${name}“ byl uložen.`);
          },
          error: err => this.handleError(err),
        });
    } else {
      this.usersApi.create({ name, username, role }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Uživatel „${name}“ byl vytvořen.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (this.isConflict(err)) {
      this.errorMessage.set("Uživatelské jméno už používá jiný uživatel.");
      return;
    }
    this.errorMessage.set(
      "Uložení selhalo. Zkontrolujte vyplněné údaje a zkuste to znovu."
    );
  }

  private isConflict(err: unknown): boolean {
    return (
      typeof err === "object" &&
      err !== null &&
      "status" in err &&
      (err as { status: number }).status === 409
    );
  }

  private reset(): void {
    this.name.set("");
    this.username.set("");
    this.role.set(Roles.Receptionist);
    this.errorMessage.set(null);
  }
}
