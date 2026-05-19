import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  CreateUserRequest,
  CreateUserResponse,
  Passkey,
  UpdateUserRequest,
  User,
  UserDetail,
} from "./users.types";
import { ApiClient } from "../api/api-client";
import type { PublicKeyCredentialCreationOptionsJSON } from "../auth/auth.types";

@Injectable({ providedIn: "root" })
export class UsersApi {
  private readonly api = inject(ApiClient);

  list(includeDisabled = true): Observable<readonly User[]> {
    return this.api.get<readonly User[]>("/users", {
      params: { includeDisabled: String(includeDisabled) },
    });
  }

  get(id: string): Observable<UserDetail> {
    return this.api.get<UserDetail>(`/users/${id}`);
  }

  create(body: CreateUserRequest): Observable<CreateUserResponse> {
    return this.api.post<CreateUserResponse>("/users", body);
  }

  update(id: string, body: UpdateUserRequest): Observable<void> {
    return this.api.put<void>(`/users/${id}`, body);
  }

  /** Soft-delete: enables lockout on the user's account. */
  disable(id: string): Observable<void> {
    return this.api.delete<void>(`/users/${id}`);
  }

  listPasskeys(userId: string): Observable<readonly Passkey[]> {
    return this.api.get<readonly Passkey[]>(`/users/${userId}/passkeys`);
  }

  revokePasskey(userId: string, passkeyId: string): Observable<void> {
    return this.api.delete<void>(`/users/${userId}/passkeys/${passkeyId}`);
  }

  registerPasskeyChallenge(
    userId: string
  ): Observable<PublicKeyCredentialCreationOptionsJSON> {
    return this.api.post<PublicKeyCredentialCreationOptionsJSON>(
      `/users/${userId}/passkeys/register/challenge`,
      {}
    );
  }

  /** `name` is the user-supplied label surfaced as `displayName` in
   *  subsequent `listPasskeys`. Pass `null` to keep the
   *  authenticator-supplied default. Backend caps at 100 chars. */
  registerPasskeyVerify(
    userId: string,
    credential: string,
    name: string | null
  ): Observable<void> {
    return this.api.post<void>(`/users/${userId}/passkeys/register/verify`, {
      credential,
      name,
    });
  }
}
