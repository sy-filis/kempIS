import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { Router } from "@angular/router";

import type { MenuItem } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { MenuModule } from "primeng/menu";

import { DashArrivalsPanel } from "./dashboard-arrivals";
import { DashCleaningPanel } from "./dashboard-cleaning";
import { DashEventsPanel } from "./dashboard-events";
import { DashMealsPanel } from "./dashboard-meals";
import { DashMetricsPanel } from "./dashboard-metrics";
import { DashMoneyPanel } from "./dashboard-money";
import { DashOutlookPanel } from "./dashboard-outlook";
import { DashOverduePanel } from "./dashboard-overdue";
import { AuthService } from "../../../core/auth/auth.service";
import { Roles } from "../../../core/auth/roles";

const LOCALE = "cs-CZ";

@Component({
  selector: "kemp-is-dashboard",
  imports: [
    ButtonModule,
    MenuModule,
    DashArrivalsPanel,
    DashCleaningPanel,
    DashEventsPanel,
    DashMealsPanel,
    DashMetricsPanel,
    DashMoneyPanel,
    DashOutlookPanel,
    DashOverduePanel,
  ],
  templateUrl: "./dashboard.page.html",
  styleUrl: "./dashboard.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly now = computed(() => {
    const d = new Date();
    const dateRaw = d.toLocaleDateString(LOCALE, {
      weekday: "long",
      day: "numeric",
      month: "long",
      year: "numeric",
    });
    const dateLong = dateRaw.charAt(0).toUpperCase() + dateRaw.slice(1);
    const time = d.toLocaleTimeString(LOCALE, {
      hour: "2-digit",
      minute: "2-digit",
    });
    return { dateLong, time };
  });

  private readonly isReceptionist = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Receptionist)
  );

  private readonly isManager = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Manager)
  );

  protected readonly createMenuItems = computed<MenuItem[]>(() => {
    const items: MenuItem[] = [
      {
        label: "Rezervace",
        icon: "pi pi-calendar-plus",
        command: () => this.goReservation(),
      },
      {
        label: "Skupinová rezervace",
        icon: "pi pi-users",
        command: () => this.goPlanWithCreate("group-reservation"),
      },
    ];
    if (this.isReceptionist()) {
      items.push({
        label: "Účtenka",
        icon: "pi pi-receipt",
        command: () => this.goBill(),
      });
    }
    if (this.isManager()) {
      items.push({
        label: "Akce",
        icon: "pi pi-megaphone",
        command: () => this.goPlanWithCreate("event"),
      });
    }
    items.push({
      label: "Mimo provoz",
      icon: "pi pi-wrench",
      command: () => this.goPlanWithCreate("out-of-order"),
    });
    return items;
  });

  private goReservation(): void {
    void this.router.navigate(["/staff/auth/desktop/reservations/new"]);
  }

  private goBill(): void {
    void this.router.navigate(["/staff/auth/desktop/bill/new"]);
  }

  private goPlanWithCreate(
    kind: "group-reservation" | "event" | "out-of-order"
  ): void {
    void this.router.navigate(["/staff/auth/desktop/reservation-plan"], {
      queryParams: { create: kind },
    });
  }
}
