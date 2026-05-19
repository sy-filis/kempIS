export type VehiclePeriod = {
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
};

export type Vehicle = {
  id: string;
  reservationId: string;
  billId: string | null;
  serviceId: string | null;
  registrationNumber: string;
  period: VehiclePeriod;
  serviceName: string;
  billNumber: string | null;
};

export type VehicleLookupRequest = {
  licencePlate: string;
};

export type VehicleLookupResponse = {
  licencePlate: string;
  /** Reservation's planned check-out date. Absent means no match. */
  checkoutAt: string; // YYYY-MM-DD
};

/** A vehicle with all three foreign keys null is a plate logged for the
 *  parking lot without any billing yet. */
export type VehicleRequest = {
  reservationId: string | null;
  billId: string | null;
  serviceId: string | null;
  registrationNumber: string;
};
