import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterOutlet,
} from "@angular/router";

import { TabsModule } from "primeng/tabs";
import { filter, map } from "rxjs/operators";

type TabDef = {
  readonly value: string;
  readonly label: string;
};

const TABS: readonly TabDef[] = [
  { value: "languages", label: "Jazyky" },
  { value: "nationalities", label: "Národnosti" },
  { value: "vat-rates", label: "Sazby DPH" },
  { value: "service-types", label: "Typy služeb" },
  { value: "services", label: "Služby" },
  { value: "service-texts", label: "Texty služeb" },
  { value: "spot-groups", label: "Skupiny míst" },
  { value: "spots", label: "Místa" },
];

@Component({
  selector: "kemp-is-system-settings",
  imports: [RouterLink, RouterOutlet, TabsModule],
  templateUrl: "./system-settings.page.html",
  styleUrl: "./system-settings.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SystemSettingsPage {
  private readonly router = inject(Router);

  protected readonly tabs = TABS;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects)
    ),
    { initialValue: this.router.url }
  );

  protected readonly activeTab = computed<string>(() => {
    const url = this.url();
    const segment = url.split("?")[0]?.split("/").pop() ?? "";
    return TABS.some(t => t.value === segment) ? segment : "languages";
  });
}
