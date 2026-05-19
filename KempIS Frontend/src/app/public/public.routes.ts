import type { Routes } from "@angular/router";

import { PublicShell } from "./public-shell/public-shell";
import { OnlineCheckinPage } from "./reservations/check-in/online-checkin.page";
import { ReservationDetailPage } from "./reservations/detail/reservation-detail.page";
import { NewReservationPage } from "./reservations/new/new-reservation.page";

export const PUBLIC_ROUTES: Routes = [
  {
    path: "",
    component: PublicShell,
    children: [
      {
        path: "reservations/new",
        component: NewReservationPage,
      },
      {
        path: "reservations/:id",
        component: ReservationDetailPage,
      },
      {
        path: "reservations/:id/check-in",
        component: OnlineCheckinPage,
      },
      {
        path: "**",
        redirectTo: "reservations/new",
      },
    ],
  },
];
