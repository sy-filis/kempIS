import type { Routes } from "@angular/router";

import { LanguagesTabPage } from "./tabs/languages-tab/languages-tab.page";
import { NationalitiesTabPage } from "./tabs/nationalities-tab/nationalities-tab.page";
import { ServiceTextsTabPage } from "./tabs/service-texts-tab/service-texts-tab.page";
import { ServiceTypesTabPage } from "./tabs/service-types-tab/service-types-tab.page";
import { ServicesTabPage } from "./tabs/services-tab/services-tab.page";
import { SpotGroupsTabPage } from "./tabs/spot-groups-tab/spot-groups-tab.page";
import { SpotsTabPage } from "./tabs/spots-tab/spots-tab.page";
import { VatRatesTabPage } from "./tabs/vat-rates-tab/vat-rates-tab.page";

export const SYSTEM_SETTINGS_ROUTES: Routes = [
  { path: "", pathMatch: "full", redirectTo: "languages" },
  { path: "languages", component: LanguagesTabPage },
  { path: "nationalities", component: NationalitiesTabPage },
  { path: "vat-rates", component: VatRatesTabPage },
  { path: "service-types", component: ServiceTypesTabPage },
  { path: "services", component: ServicesTabPage },
  { path: "service-texts", component: ServiceTextsTabPage },
  { path: "spot-groups", component: SpotGroupsTabPage },
  { path: "spots", component: SpotsTabPage },
];
