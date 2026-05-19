import { inject } from "@angular/core";
import type { CanActivateFn } from "@angular/router";
import { Router } from "@angular/router";

import { AuthService } from "./auth.service";
import { environment } from "../../../environments/environment";

export const authGuard: CanActivateFn = () => {
  if (environment.skipAuth) {
    return true;
  }
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(["/staff/login"]);
};
