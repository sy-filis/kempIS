import { inject } from "@angular/core";
import type { CanActivateChildFn, CanActivateFn } from "@angular/router";
import { Router } from "@angular/router";

import { AuthService } from "./auth.service";
import { defaultLandingFor } from "./default-landing";
import { environment } from "../../../environments/environment";

/** Reads `route.data.roles` (a `readonly string[]`) and lets
 *  activation proceed iff the current user has any of those roles.
 *  On violation redirects via `defaultLandingFor`. */
export const roleGuard: CanActivateFn & CanActivateChildFn = async route => {
  if (environment.skipAuth) {
    return true;
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.ensureCurrentUserLoaded();

  const allowed = route.data["roles"] as readonly string[] | undefined;
  if (allowed === undefined || allowed.length === 0) {
    return true;
  }
  if (auth.hasAnyRole(allowed)) {
    return true;
  }

  const userRoles = auth.currentUser()?.roles ?? [];
  return router.parseUrl(defaultLandingFor(userRoles));
};
