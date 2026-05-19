import type { Routes } from "@angular/router";

import { EdokladyPage } from "./pages/edoklady.page";
import { ScanPairPage } from "./pages/scan-pair.page";
import { SessionPage } from "./pages/session.page";
import { SignaturePage } from "./pages/signature.page";
import { WaitingPage } from "./pages/waiting.page";
import { ReceptionTabletShellComponent } from "./reception-tablet-shell.component";
import { ReceptionTabletService } from "./reception-tablet.service";
import { RealtimeClient } from "../core/reception-realtime/realtime-client";

export const RECEPTION_TABLET_ROUTES: Routes = [
  {
    path: "",
    component: ReceptionTabletShellComponent,
    providers: [RealtimeClient, ReceptionTabletService],
    children: [
      { path: "", pathMatch: "full", component: ScanPairPage },
      { path: "waiting", component: WaitingPage },
      { path: "session", component: SessionPage },
      {
        path: "session/sign/:clientGuestId",
        component: SignaturePage,
      },
      {
        path: "session/edoklady/:clientGuestId",
        component: EdokladyPage,
      },
      { path: "**", redirectTo: "" },
    ],
  },
];
