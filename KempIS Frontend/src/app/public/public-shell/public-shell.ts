import { ChangeDetectionStrategy, Component, inject } from "@angular/core";
import { RouterLink, RouterOutlet } from "@angular/router";

import { CAMP_IDENTITY } from "../../core/camp/camp-identity.token";
import { LocaleSwitcher } from "../../core/i18n/locale-switcher/locale-switcher";

@Component({
  selector: "kemp-is-public-shell",
  imports: [RouterOutlet, RouterLink, LocaleSwitcher],
  templateUrl: "./public-shell.html",
  styleUrl: "./public-shell.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicShell {
  protected readonly year = new Date().getFullYear();
  protected readonly camp = inject(CAMP_IDENTITY);
}
