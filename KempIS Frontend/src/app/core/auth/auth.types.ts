/** Subset of WebAuthn `PublicKeyCredentialRequestOptionsJSON`. Binary
 *  fields (`challenge`, descriptor `id`s) are base64url-encoded
 *  strings, matching SimpleWebAuthn-family servers. */
export type PublicKeyCredentialDescriptorJSON = {
  id: string;
  type: PublicKeyCredentialType;
  transports?: AuthenticatorTransport[];
};

export type PublicKeyCredentialRequestOptionsJSON = {
  challenge: string;
  rpId?: string;
  timeout?: number;
  userVerification?: UserVerificationRequirement;
  allowCredentials?: PublicKeyCredentialDescriptorJSON[];
};

/** Subset of WebAuthn `PublicKeyCredentialCreationOptionsJSON` produced
 *  by ASP.NET Identity's passkey challenge endpoint. Binary fields are
 *  base64url-encoded strings. */
export type PublicKeyCredentialCreationOptionsJSON = {
  rp: { id?: string; name: string };
  user: { id: string; name: string; displayName: string };
  challenge: string;
  pubKeyCredParams: { type: PublicKeyCredentialType; alg: number }[];
  timeout?: number;
  excludeCredentials?: PublicKeyCredentialDescriptorJSON[];
  authenticatorSelection?: AuthenticatorSelectionCriteria;
  attestation?: AttestationConveyancePreference;
  extensions?: Record<string, unknown>;
};

/** `clientExtensionResults` is required by ASP.NET Identity's
 *  `PublicKeyCredential<TResponse>` deserializer even when the
 *  authenticator returned no extension outputs (`{}`). */
export type RegistrationResponseJSON = {
  id: string;
  rawId: string;
  type: "public-key";
  response: {
    clientDataJSON: string;
    attestationObject: string;
    transports?: AuthenticatorTransport[];
  };
  clientExtensionResults: AuthenticationExtensionsClientOutputs;
  authenticatorAttachment: AuthenticatorAttachment | null;
};

export type AuthenticationResponseJSON = {
  id: string;
  rawId: string;
  type: "public-key";
  response: {
    clientDataJSON: string;
    authenticatorData: string;
    signature: string;
    userHandle: string | null;
  };
  clientExtensionResults: AuthenticationExtensionsClientOutputs;
  authenticatorAttachment: AuthenticatorAttachment | null;
};

export type LoginVerifyResponse = {
  accessToken: string;
  refreshToken: string;
  /** Seconds until `accessToken` expires. */
  expiresIn: number;
};

/** `sessionExpiresAt` is the absolute 12-hour re-login deadline,
 *  independent of access-token refresh. Null when the backend is running
 *  in --no-auth mode and returns a synthetic identity without a deadline. */
export type CurrentUser = {
  id: string;
  username: string;
  name: string;
  roles: readonly string[];
  sessionExpiresAt: string | null;
};

export type AuthBroadcast =
  | {
      type: "session";
      accessToken: string;
      refreshToken: string;
      accessExpiresAt: number;
    }
  | { type: "logout" }
  | { type: "request" };
