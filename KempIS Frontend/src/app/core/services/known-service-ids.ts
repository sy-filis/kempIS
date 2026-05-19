/** Keep in sync with `Domain.Services.KnownServiceIds` on the backend. */
export const KNOWN_SERVICE_IDS = {
  adult: "165cdc19-4347-4122-a58f-6d8ba172dfb7",
  child: "61737953-5677-40f1-be1b-8264d65b8b87",
  breakfast: "0bc2e7de-83c1-4023-b32f-1ea0bcfb359d",
  lunch: "f2e4ec18-c916-42fb-b0e8-912523b4e4f1",
  lunchPackage: "4d7c9b1a-2e8f-4a3d-9b15-7f6c2a8e1d92",
  dinner: "086a1440-6c30-4997-aee3-242d5157970d",
  recreationFee: "0f939342-cd59-4a4d-a9c8-22e06dee8b1a",
} as const;

export type KnownServiceKey = keyof typeof KNOWN_SERVICE_IDS;
