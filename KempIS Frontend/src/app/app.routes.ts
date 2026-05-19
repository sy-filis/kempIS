import type { Routes } from "@angular/router";

import { NotFoundPage } from "./public/not-found/not-found";

export const routes: Routes = [
  {
    path: "",
    pathMatch: "full",
    redirectTo: "public",
  },
  {
    path: "public",
    loadChildren: () =>
      import("./public/public.routes").then(m => m.PUBLIC_ROUTES),
  },
  {
    path: "staff",
    loadChildren: () =>
      import("./staff/staff.routes").then(m => m.STAFF_ROUTES),
  },
  {
    path: "reception-tablet",
    loadChildren: () =>
      import("./reception-tablet/reception-tablet.routes").then(
        m => m.RECEPTION_TABLET_ROUTES
      ),
  },
  {
    path: "**",
    component: NotFoundPage,
  },
];
