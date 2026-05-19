import { inject } from "@angular/core";
import type { Routes } from "@angular/router";

import { AccountingClosureDetailPage } from "./desktop/accounting-closures/accounting-closure-detail.page";
import { AccountingClosuresPage } from "./desktop/accounting-closures/accounting-closures.page";
import { AppSettingsPage } from "./desktop/app-settings/app-settings.page";
import { BillPage } from "./desktop/bill/bill.page";
import { BillDetailPage } from "./desktop/bill-detail/bill-detail.page";
import { BillsPage } from "./desktop/bills/bills.page";
import { DashboardPage } from "./desktop/dashboard/dashboard.page";
import { DesktopHomePage } from "./desktop/desktop-home.page";
import { GatesPage } from "./desktop/gates/gates.page";
import { GuestsPage } from "./desktop/guests/guests.page";
import { InvoiceFormPage } from "./desktop/invoices/invoice-form/invoice-form.page";
import { InvoicesPage } from "./desktop/invoices/invoices.page";
import { MealsPage } from "./desktop/meals/meals.page";
import { OperationsPage } from "./desktop/operations/operations.page";
import { ReservationPlanPage } from "./desktop/reservation-plan/reservation-plan.page";
import { ReservationFormPage } from "./desktop/reservations/reservation-form/reservation-form.page";
import { StatisticsPage } from "./desktop/statistics/statistics.page";
import { SystemSettingsPage } from "./desktop/system-settings/system-settings.page";
import { UsersPage } from "./desktop/users/users.page";
import { VehiclesPage } from "./desktop/vehicles/vehicles.page";
import { LoginPage } from "./login/login.page";
import { CleaningPage } from "./mobile/cleaning/cleaning.page";
import { MaintenancePage } from "./mobile/maintenance/maintenance.page";
import { MealsPage as MobileMealsPage } from "./mobile/meals/meals.page";
import { MobileHomePage } from "./mobile/mobile-home.page";
import { ScannerPage } from "./mobile/scanner/scanner.page";
import { SpotsPage } from "./mobile/spots/spots.page";
import { StaffShell } from "./shell/staff-shell";
import { authGuard } from "../core/auth/auth.guard";
import { AuthService } from "../core/auth/auth.service";
import { defaultLandingFor } from "../core/auth/default-landing";
import { roleGuard } from "../core/auth/role.guard";
import { Roles } from "../core/auth/roles";

const DESKTOP_ALL = [Roles.Receptionist, Roles.Accountant, Roles.Manager];
const RECEPTIONIST_OR_MANAGER = [Roles.Receptionist, Roles.Manager];

export const STAFF_ROUTES: Routes = [
  { path: "login", component: LoginPage },
  {
    path: "auth",
    component: StaffShell,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      {
        path: "",
        pathMatch: "full",
        // Functional `redirectTo` (Angular 17+). Routes by role first
        // (CleaningStaff is mobile-only), falling back to viewport for
        // staff with desktop access.
        redirectTo: (): string => {
          const auth = inject(AuthService);
          const roles = auth.currentUser()?.roles ?? [];
          if (roles.includes(Roles.CleaningStaff) && roles.length === 1) {
            return "mobile";
          }
          if (roles.includes(Roles.Accountant) && roles.length === 1) {
            return "desktop";
          }
          return matchMedia("(max-width: 768px)").matches
            ? "mobile"
            : "desktop";
        },
      },
      {
        path: "mobile",
        component: MobileHomePage,
        children: [
          { path: "", pathMatch: "full", redirectTo: "cleaning" },
          { path: "cleaning", component: CleaningPage },
          { path: "maintenance", component: MaintenancePage },
          { path: "meals", component: MobileMealsPage },
          { path: "spots", component: SpotsPage },
          { path: "scanner", component: ScannerPage },
        ],
      },
      {
        path: "desktop",
        component: DesktopHomePage,
        canActivate: [roleGuard],
        canActivateChild: [roleGuard],
        data: { roles: DESKTOP_ALL },
        children: [
          {
            path: "",
            pathMatch: "full",
            redirectTo: (): string => {
              const auth = inject(AuthService);
              const roles = auth.currentUser()?.roles ?? [];
              const target = defaultLandingFor(roles);
              return target.startsWith("/staff/auth/desktop/")
                ? target.slice("/staff/auth/desktop/".length)
                : "dashboard";
            },
          },
          {
            path: "dashboard",
            component: DashboardPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "reservation-plan",
            component: ReservationPlanPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "reservations/new",
            component: ReservationFormPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "reservations/:id/edit",
            component: ReservationFormPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "bill/new",
            component: BillPage,
            data: { roles: [Roles.Receptionist] },
          },
          {
            path: "bill/:id/edit",
            component: BillPage,
            data: { roles: [Roles.Receptionist] },
          },
          {
            path: "bills",
            component: BillsPage,
            data: { roles: DESKTOP_ALL },
          },
          {
            path: "bills/:id",
            component: BillDetailPage,
            data: { roles: DESKTOP_ALL },
          },
          {
            path: "invoices",
            component: InvoicesPage,
            data: { roles: DESKTOP_ALL },
          },
          {
            path: "invoices/new",
            component: InvoiceFormPage,
            data: { roles: [Roles.Receptionist, Roles.Manager] },
          },
          {
            path: "invoices/:invoiceId",
            component: InvoiceFormPage,
            data: { roles: DESKTOP_ALL },
          },
          {
            path: "financial-closings",
            component: AccountingClosuresPage,
            data: { roles: DESKTOP_ALL },
          },
          {
            path: "financial-closings/:id",
            component: AccountingClosureDetailPage,
            data: { roles: DESKTOP_ALL },
          },
          {
            path: "statistics",
            component: StatisticsPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "meals",
            component: MealsPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "operations",
            component: OperationsPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "guests",
            component: GuestsPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "vehicles",
            component: VehiclesPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "gates",
            component: GatesPage,
            data: { roles: [Roles.Receptionist] },
          },
          {
            path: "users",
            component: UsersPage,
            data: { roles: [Roles.Manager] },
          },
          {
            path: "app-settings",
            component: AppSettingsPage,
            data: { roles: RECEPTIONIST_OR_MANAGER },
          },
          {
            path: "system-settings",
            component: SystemSettingsPage,
            data: { roles: [Roles.Manager] },
            loadChildren: () =>
              import("./desktop/system-settings/system-settings.routes").then(
                m => m.SYSTEM_SETTINGS_ROUTES
              ),
          },
        ],
      },
      { path: "**", redirectTo: "" },
    ],
  },
  { path: "", pathMatch: "full", redirectTo: "login" },
];
