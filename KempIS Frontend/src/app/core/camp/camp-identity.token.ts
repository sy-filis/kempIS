import { InjectionToken } from "@angular/core";

import type { CampIdentity } from "../../../environments/environment.type";

/** Public-facing identity of the camp the deployment serves. Sourced
 *  from `environment.camp` and provided in `app.config.ts`. */
export const CAMP_IDENTITY = new InjectionToken<CampIdentity>("CAMP_IDENTITY");

export type { CampIdentity };
