import type { HttpInterceptorFn } from "@angular/common/http";

/** ASP.NET Identity tracks the WebAuthn challenge in an HTTP-only
 *  cookie (.AspNetCore.TwoFactorUserId) set on the challenge response
 *  and read on verify. Cross-origin fetches drop cookies unless the
 *  request opts in. */
export const passkeyCredentialsInterceptor: HttpInterceptorFn = (req, next) => {
  if (
    req.url.includes("/passkeys/register/") ||
    req.url.includes("/passkeys/login/")
  ) {
    return next(req.clone({ withCredentials: true }));
  }
  return next(req);
};
