import type {
  AuthenticationResponseJSON,
  PublicKeyCredentialCreationOptionsJSON,
  PublicKeyCredentialRequestOptionsJSON,
  RegistrationResponseJSON,
} from "./auth.types";

export function isWebAuthnSupported(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.PublicKeyCredential !== "undefined"
  );
}

export function b64uToBytes(value: string): Uint8Array<ArrayBuffer> {
  const pad = value.length % 4 === 0 ? 0 : 4 - (value.length % 4);
  const base64 = value.replace(/-/g, "+").replace(/_/g, "/") + "=".repeat(pad);
  const binary = atob(base64);
  const buffer = new ArrayBuffer(binary.length);
  const bytes = new Uint8Array(buffer);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

export function bytesToB64u(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary)
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

/** Prefers `PublicKeyCredential.toJSON()` when the browser provides it
 *  (Chromium 121+, Safari 17.4+, Firefox 119+) so the canonical shape
 *  (including `clientExtensionResults`) is emitted exactly as ASP.NET
 *  Identity's deserializer expects. */
export async function createCredential(
  options: PublicKeyCredentialCreationOptionsJSON
): Promise<RegistrationResponseJSON> {
  const publicKey: PublicKeyCredentialCreationOptions = {
    rp: options.rp,
    user: {
      id: b64uToBytes(options.user.id),
      name: options.user.name,
      displayName: options.user.displayName,
    },
    challenge: b64uToBytes(options.challenge),
    pubKeyCredParams: options.pubKeyCredParams,
    timeout: options.timeout,
    excludeCredentials: options.excludeCredentials?.map(c => ({
      id: b64uToBytes(c.id),
      type: c.type,
      transports: c.transports,
    })),
    authenticatorSelection: options.authenticatorSelection,
    attestation: options.attestation,
    extensions: options.extensions,
  };

  const credential = (await navigator.credentials.create({
    publicKey,
  })) as PublicKeyCredential | null;

  if (credential === null) {
    throw new Error("NO_CREDENTIAL");
  }

  const toJsonFn = (credential as unknown as { toJSON?: () => unknown }).toJSON;
  if (typeof toJsonFn === "function") {
    return toJsonFn.call(credential) as RegistrationResponseJSON;
  }

  const response = credential.response as AuthenticatorAttestationResponse;
  const transports =
    typeof response.getTransports === "function"
      ? (response.getTransports() as AuthenticatorTransport[])
      : undefined;

  return {
    id: credential.id,
    rawId: bytesToB64u(credential.rawId),
    type: "public-key",
    response: {
      clientDataJSON: bytesToB64u(response.clientDataJSON),
      attestationObject: bytesToB64u(response.attestationObject),
      transports,
    },
    clientExtensionResults: credential.getClientExtensionResults(),
    authenticatorAttachment:
      credential.authenticatorAttachment as AuthenticatorAttachment | null,
  };
}

/** The browser throws `DOMException` (`NotAllowedError` /
 *  `AbortError`) when the user dismisses or cancels the prompt; this
 *  additionally throws `Error("NO_CREDENTIAL")` if the call resolves
 *  to `null`. */
export async function requestAssertion(
  options: PublicKeyCredentialRequestOptionsJSON
): Promise<AuthenticationResponseJSON> {
  const publicKey: PublicKeyCredentialRequestOptions = {
    challenge: b64uToBytes(options.challenge),
    rpId: options.rpId,
    timeout: options.timeout,
    userVerification: options.userVerification,
    allowCredentials: options.allowCredentials?.map(c => ({
      id: b64uToBytes(c.id),
      type: c.type,
      transports: c.transports,
    })),
  };

  const credential = (await navigator.credentials.get({
    publicKey,
  })) as PublicKeyCredential | null;

  if (credential === null) {
    throw new Error("NO_CREDENTIAL");
  }

  const toJsonFn = (credential as unknown as { toJSON?: () => unknown }).toJSON;
  if (typeof toJsonFn === "function") {
    return toJsonFn.call(credential) as AuthenticationResponseJSON;
  }

  const response = credential.response as AuthenticatorAssertionResponse;

  return {
    id: credential.id,
    rawId: bytesToB64u(credential.rawId),
    type: "public-key",
    response: {
      clientDataJSON: bytesToB64u(response.clientDataJSON),
      authenticatorData: bytesToB64u(response.authenticatorData),
      signature: bytesToB64u(response.signature),
      userHandle:
        response.userHandle === null ? null : bytesToB64u(response.userHandle),
    },
    clientExtensionResults: credential.getClientExtensionResults(),
    authenticatorAttachment:
      credential.authenticatorAttachment as AuthenticatorAttachment | null,
  };
}
