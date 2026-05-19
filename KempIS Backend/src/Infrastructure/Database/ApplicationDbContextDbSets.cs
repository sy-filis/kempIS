using Application.Abstractions.Data;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Operations.AccessCards;
using Domain.Operations.CleanInfos;
using Domain.Operations.CleaningPlans;
using Domain.Operations.Events;
using Domain.Operations.MaintenanceIssues;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
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
using Domain.Services.Languages;
using Domain.Services.Services;
using Domain.Services.ServiceTexts;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using Infrastructure.Finance;
using Infrastructure.Reservations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public sealed partial class ApplicationDbContext
{
  public DbSet<BillItem> BillItems { get; set; }
  public DbSet<Bill> Bills { get; set; }
  public DbSet<FinancialClosing> FinancialClosings { get; set; }
  public DbSet<InvoiceItem> InvoiceItems { get; set; }
  public DbSet<Invoice> Invoices { get; set; }
  public DbSet<AccessCard> AccessCards { get; set; }
  public DbSet<CleanInfo> CleanInfos { get; set; }
  public DbSet<CleaningPlan> CleaningPlans { get; set; }
  public DbSet<Event> Events { get; set; }
  public DbSet<EventSpotGroupItem> EventSpotGroupItems { get; set; }
  public DbSet<MaintenanceIssue> MaintenanceIssues { get; set; }
  public DbSet<OutOfOrder> OutOfOrders { get; set; }
  public DbSet<SpotGroupOofItem> SpotGroupOofItems { get; set; }
  public DbSet<SpotOofItem> SpotOofItems { get; set; }
  public DbSet<GroupReservation> GroupReservations { get; set; }
  public DbSet<GroupReservationSpot> GroupReservationSpots { get; set; }
  public DbSet<Guest> Guests { get; set; }
  public DbSet<Meal> Meals { get; set; }
  public DbSet<Nationality> Nationalities { get; set; }
  public DbSet<Reservation> Reservations { get; set; }
  public DbSet<ReservationServiceItem> ReservationServiceItems { get; set; }
  public DbSet<ReservationSpotItem> ReservationSpotItems { get; set; }
  public DbSet<SpotGroup> SpotGroups { get; set; }
  public DbSet<Spot> Spots { get; set; }
  public DbSet<Vehicle> Vehicles { get; set; }
  public DbSet<Language> Languages { get; set; }
  public DbSet<Service> Services { get; set; }
  public DbSet<ServiceText> ServiceTexts { get; set; }
  public DbSet<ServiceType> ServiceTypes { get; set; }
  public DbSet<VatRate> VatRates { get; set; }

  internal DbSet<BillNumberSequence> BillNumberSequences => Set<BillNumberSequence>();
  internal DbSet<ReservationNumberSequence> ReservationNumberSequences => Set<ReservationNumberSequence>();
  internal DbSet<GroupReservationNumberSequence> GroupReservationNumberSequences => Set<GroupReservationNumberSequence>();
}
