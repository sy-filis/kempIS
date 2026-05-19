export type SpotGroup = {
  id: string;
  serviceId: string;
  name: string;
  description: string | null;
  capacity: number;
  isActive: boolean;
  imageUrl: string | null;
  detailsUrl: string | null;
};

export type Spot = {
  id: string;
  spotGroupId: string;
  name: string;
  description: string | null;
  isActive: boolean;
};

export const SpotState = {
  Unoccupied: 0,
  Occupied: 1,
  ExpectingArrival: 2,
  ExpectingDeparture: 3,
  OutOfOrder: 4,
} as const;

export type SpotState = (typeof SpotState)[keyof typeof SpotState];

export type SpotStateRecord = {
  spotId: string;
  state: SpotState;
  /** Departure date for the current occupant, when known. */
  departureDate: string | null;
  /** Always false when the spot is Unoccupied / OutOfOrder. */
  hasGivenKey: boolean;
};
