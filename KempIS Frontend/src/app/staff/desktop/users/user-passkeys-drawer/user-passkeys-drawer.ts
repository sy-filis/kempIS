import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ConfirmationService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { DrawerModule } from "primeng/drawer";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { firstValueFrom } from "rxjs";

import { createCredential } from "../../../../core/auth/passkey";
import { UsersApi } from "../../../../core/users/users.api";
import type { Passkey } from "../../../../core/users/users.types";

const MAX_NAME_LEN = 100;

@Component({
  selector: "kemp-is-user-passkeys-drawer",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    DrawerModule,
    FormsModule,
    InputTextModule,
    MessageModule,
  ],
  providers: [ConfirmationService],
  templateUrl: "./user-passkeys-drawer.html",
  styleUrl: "./user-passkeys-drawer.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserPasskeysDrawer {
  private readonly usersApi = inject(UsersApi);
  private readonly confirm = inject(ConfirmationService);

  readonly userId = input<string | null>(null);
  readonly visible = input<boolean>(false);

  readonly closed = output<void>();

  protected readonly passkeys = signal<readonly Passkey[]>([]);
  protected readonly userName = signal<string>("");
  protected readonly loading = signal<boolean>(false);
  protected readonly busy = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly newPasskeyName = signal<string>("");
  protected readonly maxNameLength = MAX_NAME_LEN;

  protected readonly hasNone = computed(
    () => !this.loading() && this.passkeys().length === 0
  );

  constructor() {
    effect(() => {
      const id = this.userId();
      if (id === null || !this.visible()) {
        return;
      }
      void this.load(id);
    });
  }

  protected onVisibleChange(visible: boolean): void {
    if (!visible) {
      this.closed.emit();
    }
  }

  private async load(id: string): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const detail = await firstValueFrom(this.usersApi.get(id));
      this.userName.set(detail.name);
      const list = await firstValueFrom(this.usersApi.listPasskeys(id));
      this.passkeys.set(list);
    } catch {
      this.errorMessage.set("Načtení klíčů se nezdařilo.");
    } finally {
      this.loading.set(false);
    }
  }

  protected async onRegister(): Promise<void> {
    const id = this.userId();
    if (id === null || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      const options = await firstValueFrom(
        this.usersApi.registerPasskeyChallenge(id)
      );
      const credential = await createCredential(options);
      const trimmed = this.newPasskeyName().trim();
      const name = trimmed.length === 0 ? null : trimmed;
      await firstValueFrom(
        this.usersApi.registerPasskeyVerify(
          id,
          JSON.stringify(credential),
          name
        )
      );
      this.newPasskeyName.set("");
      const list = await firstValueFrom(this.usersApi.listPasskeys(id));
      this.passkeys.set(list);
    } catch (err) {
      if (this.isUserCancellation(err)) {
        // User dismissed the WebAuthn prompt.
      } else {
        this.errorMessage.set(
          "Registrace klíče se nezdařila. Zkuste to znovu."
        );
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected onRevoke(passkey: Passkey): void {
    const id = this.userId();
    if (id === null) {
      return;
    }
    this.confirm.confirm({
      header: "Odebrat klíč",
      message: `Opravdu chcete odebrat klíč „${passkey.displayName ?? "bez názvu"}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Odebrat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        void this.revoke(id, passkey.id);
      },
    });
  }

  private async revoke(userId: string, passkeyId: string): Promise<void> {
    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      await firstValueFrom(this.usersApi.revokePasskey(userId, passkeyId));
      this.passkeys.update(list => list.filter(p => p.id !== passkeyId));
    } catch {
      this.errorMessage.set("Odebrání klíče se nezdařilo.");
    } finally {
      this.busy.set(false);
    }
  }

  protected formatDate(value: number): string {
    return new Date(value).toLocaleString("cs-CZ", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  private isUserCancellation(err: unknown): boolean {
    if (err instanceof DOMException) {
      return err.name === "NotAllowedError" || err.name === "AbortError";
    }
    return false;
  }
}
