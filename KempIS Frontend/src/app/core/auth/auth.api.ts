import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  CurrentUser,
  LoginVerifyResponse,
  PublicKeyCredentialRequestOptionsJSON,
} from "./auth.types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class AuthApi {
  private readonly api = inject(ApiClient);

  getLoginChallenge(): Observable<PublicKeyCredentialRequestOptionsJSON> {
    return this.api.get<PublicKeyCredentialRequestOptionsJSON>(
      "/auth/passkeys/login/challenge",
      {}
    );
  }

  /** Body is `{ credential: <JSON-stringified PublicKeyCredentialJSON> }`. */
  verifyLogin(credential: string): Observable<LoginVerifyResponse> {
    return this.api.post<LoginVerifyResponse>("/auth/passkeys/login/verify", {
      credential,
    });
  }

  refresh(refreshToken: string): Observable<LoginVerifyResponse> {
    return this.api.post<LoginVerifyResponse>("/auth/refresh", {
      refreshToken,
    });
  }

  logout(refreshToken: string): Observable<void> {
    return this.api.post<void>("/auth/logout", { refreshToken });
  }

  getMe(): Observable<CurrentUser> {
    return this.api.get<CurrentUser>("/auth/me");
  }
}
