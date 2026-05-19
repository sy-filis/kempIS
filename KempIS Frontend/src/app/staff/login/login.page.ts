import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from "@angular/core";
import { Router } from "@angular/router";

import { ButtonModule } from "primeng/button";
import { CardModule } from "primeng/card";
import { MessageModule } from "primeng/message";

import { isApiError } from "../../core/api/api-error";
import { AuthService } from "../../core/auth/auth.service";
import { LocaleSwitcher } from "../../core/i18n/locale-switcher/locale-switcher";

@Component({
  selector: "kemp-is-staff-login",
  imports: [ButtonModule, CardModule, MessageModule, LocaleSwitcher],
  templateUrl: "./login.page.html",
  styleUrl: "./login.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly pending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly title = $localize`:@@staff.login.title:Přihlášení personálu`;
  protected readonly buttonLabel = $localize`:@@staff.login.button:Přihlásit se`;

  constructor() {
    if (this.auth.isAuthenticated()) {
      void this.router.navigateByUrl("/staff/auth");
      return;
    }
    void this.attemptAutoSignIn();
  }

  // Chrome/Edge often reject WebAuthn calls without a recent user activation;
  // Safari/Firefox are more permissive. Try once silently on load.
  private async attemptAutoSignIn(): Promise<void> {
    if (this.pending()) {
      return;
    }
    this.pending.set(true);
    try {
      await this.auth.loginWithPasskey();
      await this.router.navigateByUrl("/staff/auth");
    } catch {
      // Silent on auto-attempt; onSignIn() surfaces errors after explicit click.
    } finally {
      this.pending.set(false);
    }
  }

  protected async onSignIn(): Promise<void> {
    if (this.pending()) {
      return;
    }
    this.pending.set(true);
    this.error.set(null);
    try {
      await this.auth.loginWithPasskey();
      await this.router.navigateByUrl("/staff/auth");
    } catch (err) {
      this.error.set(this.toMessage(err));
    } finally {
      this.pending.set(false);
    }
  }

  private toMessage(err: unknown): string {
    console.error("Login error", err);
    if (err instanceof Error) {
      if (err.message === "WEBAUTHN_UNSUPPORTED") {
        return $localize`:@@staff.login.error.unsupported:Tento prohlížeč nepodporuje passkey.`;
      }
      if (err.name === "NotAllowedError" || err.name === "AbortError") {
        return $localize`:@@staff.login.error.cancelled:Přihlášení bylo zrušeno.`;
      }
    }
    if (isApiError(err)) {
      return err.detail;
    }
    return $localize`:@@staff.login.error.generic:Přihlášení se nezdařilo. Zkuste to prosím znovu.`;
  }
}
