import { ChangeDetectionStrategy, Component } from "@angular/core";
import { RouterOutlet } from "@angular/router";

import { ReceptionPairingService } from "./bill/tablet-pairing/reception-pairing.service";
import { DesktopLeftNav } from "./left-nav/left-nav";
import { RealtimeClient } from "../../core/reception-realtime/realtime-client";

/** `RealtimeClient` + `ReceptionPairingService` are hosted at this level
 *  so the desktop's pairing state survives navigation between the bill
 *  page and the settings page (a tablet paired in either surface stays
 *  paired). The tablet PWA provides its own pair of these tokens in its
 *  own route tree, so the two roles do not share an instance. */
@Component({
  selector: "kemp-is-staff-desktop-home",
  imports: [RouterOutlet, DesktopLeftNav],
  templateUrl: "./desktop-home.page.html",
  styleUrl: "./desktop-home.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [RealtimeClient, ReceptionPairingService],
})
export class DesktopHomePage {}
