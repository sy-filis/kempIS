import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Router, RouterLink, RouterLinkActive } from "@angular/router";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { DialogModule } from "primeng/dialog";
import { ToastModule } from "primeng/toast";

import { AuthService } from "../../../core/auth/auth.service";
import { Roles } from "../../../core/auth/roles";
import { CAMP_IDENTITY } from "../../../core/camp/camp-identity.token";
import {
  formatRelativeAgo,
  RefreshController,
} from "../../../core/refresh/refresh-controller";
import { FinancialClosingsApi } from "../../api/financial-closings.api";
import { GuestsApi } from "../../api/guests.api";
import { PendingReservationsStore } from "../../api/pending-reservations.store";
import { formatCzk } from "../accounting-closures/accounting-closures-data";

type NavItem = {
  readonly id: string;
  readonly label: string;
  readonly icon: string;
  readonly link: string;
  readonly badge?: number;
  readonly roles: readonly string[];
};

const RECEPTIONIST_OR_MANAGER = [Roles.Receptionist, Roles.Manager];
const DESKTOP_ALL = [Roles.Receptionist, Roles.Accountant, Roles.Manager];

const ROLE_LABEL: Record<string, string> = {
  [Roles.Guest]: "Host",
  [Roles.CleaningStaff]: "Úklid",
  [Roles.Accountant]: "Účetní",
  [Roles.Receptionist]: "Recepční",
  [Roles.Manager]: "Manažer",
};

/** Highest-priority role first; used to pick a single label when a user holds several roles. */
const ROLE_PRIORITY: readonly string[] = [
  Roles.Manager,
  Roles.Accountant,
  Roles.Receptionist,
  Roles.CleaningStaff,
  Roles.Guest,
];

const NAV_ITEMS: readonly NavItem[] = [
  {
    id: "dashboard",
    label: "Dashboard",
    icon: "pi-th-large",
    link: "dashboard",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "rezervace",
    label: "Rezervace",
    icon: "pi-calendar",
    link: "reservation-plan",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "nova-uctenka",
    label: "Nová účtenka",
    icon: "pi-receipt",
    link: "bill/new",
    roles: [Roles.Receptionist],
  },
  {
    id: "vystavene-uctenky",
    label: "Vystavené účtenky",
    icon: "pi-file",
    link: "bills",
    roles: DESKTOP_ALL,
  },
  {
    id: "vystavene-faktury",
    label: "Vystavené faktury",
    icon: "pi-file-edit",
    link: "invoices",
    roles: DESKTOP_ALL,
  },
  {
    id: "stravovani",
    label: "Stravování",
    icon: "pi-apple",
    link: "meals",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "provoz",
    label: "Provoz",
    icon: "pi-wrench",
    link: "operations",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "hoste",
    label: "Hosté",
    icon: "pi-users",
    link: "guests",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "vozidla",
    label: "Vozidla",
    icon: "pi-car",
    link: "vehicles",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "zavory",
    label: "Závory",
    icon: "pi-lock",
    link: "gates",
    roles: [Roles.Receptionist],
  },
  {
    id: "ucetni-zaverky",
    label: "Účetní závěrky",
    icon: "pi-book",
    link: "financial-closings",
    roles: DESKTOP_ALL,
  },
  {
    id: "statistiky",
    label: "Statistiky",
    icon: "pi-chart-line",
    link: "statistics",
    roles: RECEPTIONIST_OR_MANAGER,
  },
  {
    id: "uzivatele",
    label: "Uživatelé",
    icon: "pi-id-card",
    link: "users",
    roles: [Roles.Manager],
  },
  {
    id: "nastaveni-systemu",
    label: "Nastavení systému",
    icon: "pi-cog",
    link: "system-settings",
    roles: [Roles.Manager],
  },
  {
    id: "nastaveni-aplikace",
    label: "Nastavení aplikace",
    icon: "pi-sliders-h",
    link: "app-settings",
    roles: RECEPTIONIST_OR_MANAGER,
  },
];

type PoliceState = "idle" | "pending" | "ok" | "error";
type ClosingState =
  | { kind: "idle" }
  | { kind: "pending" }
  | { kind: "ok"; id: string; billCount: number; totalAmount: number }
  | { kind: "empty" }
  | { kind: "error" };

@Component({
  selector: "kemp-is-desktop-left-nav",
  imports: [
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    DialogModule,
    ToastModule,
  ],
  templateUrl: "./left-nav.html",
  styleUrl: "./left-nav.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class DesktopLeftNav {
  private readonly auth = inject(AuthService);
  private readonly refresh = inject(RefreshController);
  private readonly pendingReservations = inject(PendingReservationsStore);
  private readonly guestsApi = inject(GuestsApi);
  private readonly closingsApi = inject(FinancialClosingsApi);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly now = signal(Date.now());

  protected readonly cancelShiftDialogVisible = signal(false);

  protected readonly formatCzk = formatCzk;

  /** Police report and financial closing fire sequentially but independently. */
  protected readonly policeState = signal<PoliceState>("idle");
  protected readonly closingState = signal<ClosingState>({ kind: "idle" });

  protected readonly shiftRunning = computed<boolean>(
    () =>
      this.policeState() === "pending" || this.closingState().kind === "pending"
  );

  protected readonly shiftFinished = computed<boolean>(() => {
    const p = this.policeState();
    const c = this.closingState().kind;
    return (
      (p === "ok" || p === "error") &&
      (c === "ok" || c === "empty" || c === "error")
    );
  });

  protected readonly brand = "KempIS";
  protected readonly brandSubtitle = `${inject(CAMP_IDENTITY).name} · recepce`;

  protected readonly items = computed<readonly NavItem[]>(() => {
    const userRoles = this.auth.currentUser()?.roles;
    if (userRoles === undefined) {
      return [];
    }
    const pendingCount = this.pendingReservations.count();
    return NAV_ITEMS.filter(item =>
      item.roles.some(r => userRoles.includes(r))
    ).map(item =>
      item.id === "rezervace" && pendingCount > 0
        ? { ...item, badge: pendingCount }
        : item
    );
  });

  protected readonly userName = computed(
    () => this.auth.currentUser()?.name ?? ""
  );

  protected readonly userRole = computed(() => {
    const roles = this.auth.currentUser()?.roles;
    if (!roles || roles.length === 0) {
      return "";
    }
    const role = ROLE_PRIORITY.find(r => roles.includes(r)) ?? roles[0]!;
    return ROLE_LABEL[role] ?? role;
  });

  protected readonly userInitials = computed(() => {
    const name = this.userName();
    const parts = name.trim().split(/\s+/).filter(Boolean);
    const first = parts[0]?.[0] ?? "";
    const second = parts[1]?.[0] ?? "";
    return (first + second).toUpperCase();
  });

  protected readonly remainingTime = computed(() => {
    const exp = this.auth.currentUser()?.sessionExpiresAt;
    if (exp === undefined || exp === null) {
      return "";
    }
    const remainingMs = Math.max(0, Date.parse(exp) - this.now());
    const totalSec = Math.floor(remainingMs / 1000);
    const h = Math.floor(totalSec / 3600);
    const m = Math.floor((totalSec % 3600) / 60);
    const s = totalSec % 60;
    return `${h}.${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  });

  protected readonly refreshAgo = computed(() =>
    formatRelativeAgo(this.refresh.now(), this.refresh.lastRefreshAt())
  );

  constructor() {
    const tick = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(tick));
  }

  protected onLogout(): void {
    void this.auth.logout();
  }

  protected onRefresh(): void {
    this.refresh.refreshNow();
  }

  protected openCancelShift(): void {
    this.policeState.set("idle");
    this.closingState.set({ kind: "idle" });
    this.cancelShiftDialogVisible.set(true);
  }

  protected closeCancelShift(): void {
    if (this.shiftRunning()) {
      return;
    }
    this.cancelShiftDialogVisible.set(false);
  }

  /** Financial closing fires whether or not the police report succeeded - Ubyport failure must not block cash reconciliation. */
  protected runEndOfShift(): void {
    if (this.shiftRunning()) {
      return;
    }
    this.policeState.set("pending");
    this.closingState.set({ kind: "pending" });

    this.guestsApi
      .reportToPolice()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.policeState.set("ok");
          this.runClosing();
        },
        error: () => {
          this.policeState.set("error");
          this.runClosing();
        },
      });
  }

  private runClosing(): void {
    this.closingsApi
      .create()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: response => {
          this.closingState.set({
            kind: "ok",
            id: response.id,
            billCount: response.billCount,
            totalAmount: response.totalAmount,
          });
        },
        error: (err: unknown) => {
          const status =
            typeof err === "object" && err !== null && "status" in err
              ? (err as { status: unknown }).status
              : null;
          if (status === 409) {
            this.closingState.set({ kind: "empty" });
          } else {
            this.closingState.set({ kind: "error" });
            this.messages.add({
              severity: "error",
              summary: "Účetní závěrka",
              detail: "Závěrku se nepodařilo vytvořit.",
            });
          }
        },
      });
  }

  protected onOpenClosing(): void {
    const state = this.closingState();
    if (state.kind !== "ok") {
      return;
    }
    this.cancelShiftDialogVisible.set(false);
    void this.router.navigate([
      "/staff/auth/desktop/financial-closings",
      state.id,
    ]);
  }
}
