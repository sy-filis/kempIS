using Domain.Reservations.GroupReservations;
using Domain.Reservations.Guests;
using Domain.Reservations.Meals;
using Domain.Reservations.Nationalities;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationServiceItems;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using Domain.Reservations.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public partial interface IApplicationDbContext
{
  DbSet<GroupReservation> GroupReservations { get; }
  DbSet<GroupReservationSpot> GroupReservationSpots { get; }
  DbSet<Guest> Guests { get; }
  DbSet<Meal> Meals { get; }
  DbSet<Nationality> Nationalities { get; }
  DbSet<Reservation> Reservations { get; }
  DbSet<ReservationServiceItem> ReservationServiceItems { get; }
  DbSet<ReservationSpotItem> ReservationSpotItems { get; }
  DbSet<SpotGroup> SpotGroups { get; }
  DbSet<Spot> Spots { get; }
  DbSet<Vehicle> Vehicles { get; }
}
