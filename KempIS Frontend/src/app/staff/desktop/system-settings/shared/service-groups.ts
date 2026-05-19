export const ServiceGroup = {
  Persons: 0,
  Vehicles: 1,
  MotorHomes: 2,
  Tents: 3,
  Meals: 4,
  Spots: 5,
  RecreationFees: 6,
  Others: 7,
} as const;

export type ServiceGroup = (typeof ServiceGroup)[keyof typeof ServiceGroup];

export const SERVICE_GROUP_LABELS: Record<ServiceGroup, string> = {
  [ServiceGroup.Persons]: "Osoby",
  [ServiceGroup.Vehicles]: "Vozidla",
  [ServiceGroup.MotorHomes]: "Obytné vozy",
  [ServiceGroup.Tents]: "Stany",
  [ServiceGroup.Meals]: "Jídlo",
  [ServiceGroup.Spots]: "Místa",
  [ServiceGroup.RecreationFees]: "Rekreační poplatky",
  [ServiceGroup.Others]: "Ostatní",
};

export const SERVICE_GROUP_OPTIONS: readonly {
  label: string;
  value: ServiceGroup;
}[] = Object.entries(SERVICE_GROUP_LABELS)
  .map(([key, label]) => ({ label, value: Number(key) as ServiceGroup }))
  .sort((a, b) => a.value - b.value);
