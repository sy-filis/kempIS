export type CampIdentity = {
  name: string;
  phoneDisplay: string;
  // Digits + leading "+", no spaces; used for tel: hrefs.
  phoneTel: string;
  email: string;
  address: {
    street: string;
    city: string;
    zip: string;
  };
};

export type Environment = {
  apiBaseUrl: string;
  camp: CampIdentity;
  /** When `true`, the staff route guards short-circuit to `true` so every
   *  `/staff/auth/**` page can be reached without a valid session. Intended
   *  for first-run setup (no users exist yet); leave `false` in production. */
  skipAuth: boolean;
};
