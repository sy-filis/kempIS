import { ChangeDetectionStrategy, Component } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";

type NavItem = {
  readonly id: string;
  readonly label: string;
  readonly icon: string;
  readonly link: string;
};

@Component({
  selector: "kemp-is-staff-mobile-home",
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./mobile-home.page.html",
  styleUrl: "./mobile-home.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MobileHomePage {
  protected readonly navItems: readonly NavItem[] = [
    { id: "cleaning", label: "Úklid", icon: "pi-sparkles", link: "cleaning" },
    {
      id: "maintenance",
      label: "Údržba",
      icon: "pi-wrench",
      link: "maintenance",
    },
    { id: "meals", label: "Strava", icon: "pi-apple", link: "meals" },
    { id: "spots", label: "Místa", icon: "pi-map-marker", link: "spots" },
    { id: "scanner", label: "Skener", icon: "pi-qrcode", link: "scanner" },
  ];
}
