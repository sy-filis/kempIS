/** Mirrors `Application/Abstractions/Authentication/Roles.cs` on the
 *  backend - keep in sync. */
export const Roles = {
  Guest: "Guest",
  Receptionist: "Receptionist",
  Accountant: "Accountant",
  CleaningStaff: "CleaningStaff",
  Manager: "Manager",
} as const;

export type RoleName = (typeof Roles)[keyof typeof Roles];
