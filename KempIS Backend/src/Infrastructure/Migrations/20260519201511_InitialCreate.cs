using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
  /// <inheritdoc />
  public partial class InitialCreate : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.EnsureSchema(
          name: "public");

      migrationBuilder.CreateTable(
          name: "AspNetRoles",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            concurrency_stamp = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_roles", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUsers",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "text", nullable: false),
            user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
            password_hash = table.Column<string>(type: "text", nullable: true),
            security_stamp = table.Column<string>(type: "text", nullable: true),
            concurrency_stamp = table.Column<string>(type: "text", nullable: true),
            phone_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
            two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
            lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
            access_failed_count = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_users", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "BillNumberSequences",
          schema: "public",
          columns: table => new
          {
            year = table.Column<int>(type: "integer", nullable: false),
            last_seq = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_bill_number_sequences", x => x.year);
          });

      migrationBuilder.CreateTable(
          name: "CleaningPlans",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            date = table.Column<DateOnly>(type: "date", nullable: false),
            updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_cleaning_plans", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "Events",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            starts_at = table.Column<DateOnly>(type: "date", nullable: false),
            ends_at = table.Column<DateOnly>(type: "date", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_events", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "FinancialClosings",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            financial_closing_id = table.Column<long>(type: "bigint", nullable: false),
            total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
            document_content = table.Column<byte[]>(type: "bytea", nullable: true),
            document_generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_financial_closings", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "GroupReservationNumberSequences",
          schema: "public",
          columns: table => new
          {
            year = table.Column<int>(type: "integer", nullable: false),
            last_seq = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_group_reservation_number_sequences", x => x.year);
          });

      migrationBuilder.CreateTable(
          name: "GroupReservations",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            state = table.Column<string>(type: "text", nullable: false),
            secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            organizer_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            organizer_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            organizer_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "cs"),
            DateRangeFrom = table.Column<DateOnly>(type: "date", nullable: false),
            DateRangeTo = table.Column<DateOnly>(type: "date", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_group_reservations", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "Languages",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
            name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_languages", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "OutOfOrders",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
            DateRangeFrom = table.Column<DateOnly>(type: "date", nullable: false),
            DateRangeTo = table.Column<DateOnly>(type: "date", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_out_of_orders", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "ReservationNumberSequences",
          schema: "public",
          columns: table => new
          {
            year = table.Column<int>(type: "integer", nullable: false),
            last_seq = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_reservation_number_sequences", x => x.year);
          });

      migrationBuilder.CreateTable(
          name: "ServiceTypes",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_service_types", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "VatRates",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_vat_rates", x => x.id);
          });

      migrationBuilder.CreateTable(
          name: "AspNetRoleClaims",
          schema: "public",
          columns: table => new
          {
            id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            role_id = table.Column<Guid>(type: "uuid", nullable: false),
            claim_type = table.Column<string>(type: "text", nullable: true),
            claim_value = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
            table.ForeignKey(
                      name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                      column: x => x.role_id,
                      principalSchema: "public",
                      principalTable: "AspNetRoles",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserClaims",
          schema: "public",
          columns: table => new
          {
            id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            claim_type = table.Column<string>(type: "text", nullable: true),
            claim_value = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
            table.ForeignKey(
                      name: "fk_asp_net_user_claims_asp_net_users_user_id",
                      column: x => x.user_id,
                      principalSchema: "public",
                      principalTable: "AspNetUsers",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserLogins",
          schema: "public",
          columns: table => new
          {
            login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            provider_display_name = table.Column<string>(type: "text", nullable: true),
            user_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
            table.ForeignKey(
                      name: "fk_asp_net_user_logins_asp_net_users_user_id",
                      column: x => x.user_id,
                      principalSchema: "public",
                      principalTable: "AspNetUsers",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserPasskeys",
          schema: "public",
          columns: table => new
          {
            credential_id = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            data = table.Column<string>(type: "jsonb", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_user_passkeys", x => x.credential_id);
            table.ForeignKey(
                      name: "fk_asp_net_user_passkeys_asp_net_users_user_id",
                      column: x => x.user_id,
                      principalSchema: "public",
                      principalTable: "AspNetUsers",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserRoles",
          schema: "public",
          columns: table => new
          {
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            role_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
            table.ForeignKey(
                      name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                      column: x => x.role_id,
                      principalSchema: "public",
                      principalTable: "AspNetRoles",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_asp_net_user_roles_asp_net_users_user_id",
                      column: x => x.user_id,
                      principalSchema: "public",
                      principalTable: "AspNetUsers",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "AspNetUserTokens",
          schema: "public",
          columns: table => new
          {
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            value = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
            table.ForeignKey(
                      name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                      column: x => x.user_id,
                      principalSchema: "public",
                      principalTable: "AspNetUsers",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "Reservations",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
            group_reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
            state = table.Column<string>(type: "text", nullable: false),
            created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            online_check_in_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "cs"),
            DateRangeFrom = table.Column<DateOnly>(type: "date", nullable: false),
            DateRangeTo = table.Column<DateOnly>(type: "date", nullable: false),
            reservation_maker_email = table.Column<string>(type: "text", nullable: false),
            reservation_maker_name = table.Column<string>(type: "text", nullable: false),
            reservation_maker_phone = table.Column<string>(type: "text", nullable: false),
            reservation_maker_surname = table.Column<string>(type: "text", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_reservations", x => x.id);
            table.ForeignKey(
                      name: "fk_reservations_group_reservations_group_reservation_id",
                      column: x => x.group_reservation_id,
                      principalSchema: "public",
                      principalTable: "GroupReservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.SetNull);
          });

      migrationBuilder.CreateTable(
          name: "Nationalities",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            alpha2 = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
            alpha3 = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
            numeric = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
            visa_required = table.Column<bool>(type: "boolean", nullable: false),
            biometrics_required = table.Column<bool>(type: "boolean", nullable: false),
            is_eu = table.Column<bool>(type: "boolean", nullable: false),
            language_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_nationalities", x => x.id);
            table.ForeignKey(
                      name: "fk_nationalities_languages_language_id",
                      column: x => x.language_id,
                      principalSchema: "public",
                      principalTable: "Languages",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Services",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            service_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
            vat_rate_id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            base_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_services", x => x.id);
            table.ForeignKey(
                      name: "fk_services_service_types_service_type_id",
                      column: x => x.service_type_id,
                      principalSchema: "public",
                      principalTable: "ServiceTypes",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_services_vat_rates_vat_rate_id",
                      column: x => x.vat_rate_id,
                      principalSchema: "public",
                      principalTable: "VatRates",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Meals",
          schema: "public",
          columns: table => new
          {
            reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
            date = table.Column<DateOnly>(type: "date", nullable: false),
            breakfast_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
            breakfast_normal = table.Column<long>(type: "bigint", nullable: false),
            breakfast_gluten_free = table.Column<long>(type: "bigint", nullable: false),
            breakfast_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            breakfast_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            breakfast_gluten_free_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            breakfast_gluten_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            breakfast_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            breakfast_gluten_free_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
            lunch_normal = table.Column<long>(type: "bigint", nullable: false),
            lunch_gluten_free = table.Column<long>(type: "bigint", nullable: false),
            lunch_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            lunch_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_gluten_free_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            lunch_gluten_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_gluten_free_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
            lunch_package_normal = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_gluten_free = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_gluten_free_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_gluten_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            lunch_package_gluten_free_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            dinner_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
            dinner_normal = table.Column<long>(type: "bigint", nullable: false),
            dinner_gluten_free = table.Column<long>(type: "bigint", nullable: false),
            dinner_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            dinner_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            dinner_gluten_free_lactose_free = table.Column<long>(type: "bigint", nullable: false),
            dinner_gluten_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            dinner_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false),
            dinner_gluten_free_lactose_free_vegetarian = table.Column<long>(type: "bigint", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_meals", x => new { x.reservation_id, x.date });
            table.ForeignKey(
                      name: "fk_meals_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "Bills",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
            kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            original_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
            repair_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            language_id_guid = table.Column<Guid>(type: "uuid", nullable: false),
            financial_closing_id = table.Column<Guid>(type: "uuid", nullable: true),
            number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            check_in_at = table.Column<DateOnly>(type: "date", nullable: false),
            check_out_at = table.Column<DateOnly>(type: "date", nullable: false),
            document_content = table.Column<byte[]>(type: "bytea", nullable: true),
            document_generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            payer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            payer_surname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            payer_address_country_id = table.Column<Guid>(type: "uuid", nullable: false),
            payer_address_city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            payer_address_zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            payer_address_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            payer_address_house_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            legal_entity_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            legal_entity_address_country_id = table.Column<Guid>(type: "uuid", nullable: true),
            legal_entity_address_city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            legal_entity_address_zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            legal_entity_address_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            legal_entity_address_house_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            legal_entity_cin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            legal_entity_tin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            payment_payment_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            payment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
            scartation = table.Column<DateOnly>(type: "date", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_bills", x => x.id);
            table.ForeignKey(
                      name: "fk_bills_bills_original_bill_id",
                      column: x => x.original_bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_bills_nationalities_legal_entity_address_country_id",
                      column: x => x.legal_entity_address_country_id,
                      principalSchema: "public",
                      principalTable: "Nationalities",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_bills_nationalities_payer_address_country_id",
                      column: x => x.payer_address_country_id,
                      principalSchema: "public",
                      principalTable: "Nationalities",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_bills_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "ReservationServiceItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
            service_id = table.Column<Guid>(type: "uuid", nullable: false),
            quantity = table.Column<long>(type: "bigint", nullable: false),
            recap_single_quantity = table.Column<long>(type: "bigint", nullable: false),
            recap_day_quantity = table.Column<long>(type: "bigint", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_reservation_service_items", x => x.id);
            table.ForeignKey(
                      name: "fk_reservation_service_items_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_reservation_service_items_services_service_id",
                      column: x => x.service_id,
                      principalSchema: "public",
                      principalTable: "Services",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "ServiceTexts",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            service_id = table.Column<Guid>(type: "uuid", nullable: false),
            language_id = table.Column<Guid>(type: "uuid", nullable: false),
            print_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_service_texts", x => x.id);
            table.ForeignKey(
                      name: "fk_service_texts_languages_language_id",
                      column: x => x.language_id,
                      principalSchema: "public",
                      principalTable: "Languages",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_service_texts_services_service_id",
                      column: x => x.service_id,
                      principalSchema: "public",
                      principalTable: "Services",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "SpotGroups",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            service_id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            capacity = table.Column<long>(type: "bigint", nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false),
            image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
            details_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_spot_groups", x => x.id);
            table.ForeignKey(
                      name: "fk_spot_groups_services_service_id",
                      column: x => x.service_id,
                      principalSchema: "public",
                      principalTable: "Services",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "AccessCards",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            uid = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
            bill_id = table.Column<Guid>(type: "uuid", nullable: true),
            deposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
            valid_until = table.Column<DateOnly>(type: "date", nullable: false),
            issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            note = table.Column<string>(type: "text", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_access_cards", x => x.id);
            table.ForeignKey(
                      name: "fk_access_cards_bills_bill_id",
                      column: x => x.bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "BillItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            bill_id = table.Column<Guid>(type: "uuid", nullable: false),
            service_id = table.Column<Guid>(type: "uuid", nullable: true),
            quantity = table.Column<long>(type: "bigint", nullable: false),
            unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
            vat_rate_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            recap_single_quantity = table.Column<long>(type: "bigint", nullable: false),
            recap_day_quantity = table.Column<long>(type: "bigint", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_bill_items", x => x.id);
            table.ForeignKey(
                      name: "fk_bill_items_bills_bill_id",
                      column: x => x.bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "Guests",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
            bill_id = table.Column<Guid>(type: "uuid", nullable: true),
            pays_recreation_fee = table.Column<bool>(type: "boolean", nullable: true),
            first_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            last_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            nationality_id = table.Column<Guid>(type: "uuid", nullable: false),
            date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
            document_type = table.Column<int>(type: "integer", nullable: true),
            document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
            address_country_id = table.Column<Guid>(type: "uuid", nullable: false),
            address_city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            address_zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            address_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            address_house_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            reason_of_stay = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            visa_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
            note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            scartation = table.Column<DateOnly>(type: "date", nullable: true),
            check_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            check_out_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            signature_png = table.Column<byte[]>(type: "bytea", maxLength: 1048576, nullable: true),
            signature_captured_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            DateRangeFrom = table.Column<DateOnly>(type: "date", nullable: true),
            DateRangeTo = table.Column<DateOnly>(type: "date", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_guests", x => x.id);
            table.ForeignKey(
                      name: "fk_guests_bills_bill_id",
                      column: x => x.bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.SetNull);
            table.ForeignKey(
                      name: "fk_guests_nationalities_address_country_id",
                      column: x => x.address_country_id,
                      principalSchema: "public",
                      principalTable: "Nationalities",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_guests_nationalities_nationality_id",
                      column: x => x.nationality_id,
                      principalSchema: "public",
                      principalTable: "Nationalities",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_guests_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.SetNull);
          });

      migrationBuilder.CreateTable(
          name: "Invoices",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
            number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
            status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            issued_at = table.Column<DateOnly>(type: "date", nullable: false),
            paid_at = table.Column<DateOnly>(type: "date", nullable: true),
            due_to = table.Column<DateOnly>(type: "date", nullable: true),
            linked_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
            email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
            phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            payer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            payer_surname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            payer_address_country_id = table.Column<Guid>(type: "uuid", nullable: true),
            payer_address_city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            payer_address_zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            payer_address_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            payer_address_house_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            legal_entity_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            legal_entity_address_country_id = table.Column<Guid>(type: "uuid", nullable: true),
            legal_entity_address_city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            legal_entity_address_zip_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            legal_entity_address_street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            legal_entity_address_house_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            legal_entity_cin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            legal_entity_tin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
            scartation = table.Column<DateOnly>(type: "date", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_invoices", x => x.id);
            table.ForeignKey(
                      name: "fk_invoices_bills_linked_bill_id",
                      column: x => x.linked_bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_invoices_nationalities_legal_entity_address_country_id",
                      column: x => x.legal_entity_address_country_id,
                      principalSchema: "public",
                      principalTable: "Nationalities",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_invoices_nationalities_payer_address_country_id",
                      column: x => x.payer_address_country_id,
                      principalSchema: "public",
                      principalTable: "Nationalities",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_invoices_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Vehicles",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
            bill_id = table.Column<Guid>(type: "uuid", nullable: true),
            service_id = table.Column<Guid>(type: "uuid", nullable: true),
            registration_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_vehicles", x => x.id);
            table.ForeignKey(
                      name: "fk_vehicles_bills_bill_id",
                      column: x => x.bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_vehicles_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.SetNull);
            table.ForeignKey(
                      name: "fk_vehicles_services_service_id",
                      column: x => x.service_id,
                      principalSchema: "public",
                      principalTable: "Services",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "EventSpotGroupItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            event_id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_group_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_event_spot_group_items", x => x.id);
            table.ForeignKey(
                      name: "fk_event_spot_group_items_events_event_id",
                      column: x => x.event_id,
                      principalSchema: "public",
                      principalTable: "Events",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_event_spot_group_items_spot_groups_spot_group_id",
                      column: x => x.spot_group_id,
                      principalSchema: "public",
                      principalTable: "SpotGroups",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "SpotGroupOofItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_group_id = table.Column<Guid>(type: "uuid", nullable: false),
            out_of_order_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_spot_group_oof_items", x => x.id);
            table.ForeignKey(
                      name: "fk_spot_group_oof_items_out_of_orders_out_of_order_id",
                      column: x => x.out_of_order_id,
                      principalSchema: "public",
                      principalTable: "OutOfOrders",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_spot_group_oof_items_spot_groups_spot_group_id",
                      column: x => x.spot_group_id,
                      principalSchema: "public",
                      principalTable: "SpotGroups",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Spots",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_group_id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            is_active = table.Column<bool>(type: "boolean", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_spots", x => x.id);
            table.ForeignKey(
                      name: "fk_spots_spot_groups_spot_group_id",
                      column: x => x.spot_group_id,
                      principalSchema: "public",
                      principalTable: "SpotGroups",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "InvoiceItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
            service_guid = table.Column<Guid>(type: "uuid", nullable: false),
            quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
            unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
            vat_rate_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_invoice_items", x => x.id);
            table.ForeignKey(
                      name: "fk_invoice_items_invoices_invoice_id",
                      column: x => x.invoice_id,
                      principalSchema: "public",
                      principalTable: "Invoices",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "CleanInfos",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            cleaning_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_id = table.Column<Guid>(type: "uuid", nullable: false),
            responsible_user_id = table.Column<Guid>(type: "uuid", nullable: true),
            completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_clean_infos", x => x.id);
            table.ForeignKey(
                      name: "fk_clean_infos_cleaning_plans_cleaning_plan_id",
                      column: x => x.cleaning_plan_id,
                      principalSchema: "public",
                      principalTable: "CleaningPlans",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_clean_infos_spots_spot_id",
                      column: x => x.spot_id,
                      principalSchema: "public",
                      principalTable: "Spots",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "GroupReservationSpots",
          schema: "public",
          columns: table => new
          {
            group_reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_group_reservation_spots", x => new { x.group_reservation_id, x.spot_id });
            table.ForeignKey(
                      name: "fk_group_reservation_spots_group_reservations_group_reservation_id",
                      column: x => x.group_reservation_id,
                      principalSchema: "public",
                      principalTable: "GroupReservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_group_reservation_spots_spots_spot_id",
                      column: x => x.spot_id,
                      principalSchema: "public",
                      principalTable: "Spots",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "maintenance_issues",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_id = table.Column<Guid>(type: "uuid", nullable: true),
            issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            problem_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
            solver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
            resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_maintenance_issues", x => x.id);
            table.ForeignKey(
                      name: "fk_maintenance_issues_asp_net_users_solver_user_id",
                      column: x => x.solver_user_id,
                      principalSchema: "public",
                      principalTable: "AspNetUsers",
                      principalColumn: "id",
                      onDelete: ReferentialAction.SetNull);
            table.ForeignKey(
                      name: "fk_maintenance_issues_spots_spot_id",
                      column: x => x.spot_id,
                      principalSchema: "public",
                      principalTable: "Spots",
                      principalColumn: "id",
                      onDelete: ReferentialAction.SetNull);
          });

      migrationBuilder.CreateTable(
          name: "ReservationSpotItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_group_id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_id = table.Column<Guid>(type: "uuid", nullable: true),
            has_given_key = table.Column<bool>(type: "boolean", nullable: false),
            has_returned_keys = table.Column<bool>(type: "boolean", nullable: false),
            bill_id = table.Column<Guid>(type: "uuid", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_reservation_spot_items", x => x.id);
            table.ForeignKey(
                      name: "fk_reservation_spot_items_bills_bill_id",
                      column: x => x.bill_id,
                      principalSchema: "public",
                      principalTable: "Bills",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_reservation_spot_items_reservations_reservation_id",
                      column: x => x.reservation_id,
                      principalSchema: "public",
                      principalTable: "Reservations",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_reservation_spot_items_spot_groups_spot_group_id",
                      column: x => x.spot_group_id,
                      principalSchema: "public",
                      principalTable: "SpotGroups",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "fk_reservation_spot_items_spots_spot_id",
                      column: x => x.spot_id,
                      principalSchema: "public",
                      principalTable: "Spots",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "SpotOofItems",
          schema: "public",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            spot_id = table.Column<Guid>(type: "uuid", nullable: false),
            out_of_order_id = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("pk_spot_oof_items", x => x.id);
            table.ForeignKey(
                      name: "fk_spot_oof_items_out_of_orders_out_of_order_id",
                      column: x => x.out_of_order_id,
                      principalSchema: "public",
                      principalTable: "OutOfOrders",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "fk_spot_oof_items_spots_spot_id",
                      column: x => x.spot_id,
                      principalSchema: "public",
                      principalTable: "Spots",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateIndex(
          name: "ix_access_cards_bill_id",
          schema: "public",
          table: "AccessCards",
          column: "bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_access_cards_uid",
          schema: "public",
          table: "AccessCards",
          column: "uid",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_asp_net_role_claims_role_id",
          schema: "public",
          table: "AspNetRoleClaims",
          column: "role_id");

      migrationBuilder.CreateIndex(
          name: "RoleNameIndex",
          schema: "public",
          table: "AspNetRoles",
          column: "normalized_name",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_asp_net_user_claims_user_id",
          schema: "public",
          table: "AspNetUserClaims",
          column: "user_id");

      migrationBuilder.CreateIndex(
          name: "ix_asp_net_user_logins_user_id",
          schema: "public",
          table: "AspNetUserLogins",
          column: "user_id");

      migrationBuilder.CreateIndex(
          name: "ix_asp_net_user_passkeys_user_id",
          schema: "public",
          table: "AspNetUserPasskeys",
          column: "user_id");

      migrationBuilder.CreateIndex(
          name: "ix_asp_net_user_roles_role_id",
          schema: "public",
          table: "AspNetUserRoles",
          column: "role_id");

      migrationBuilder.CreateIndex(
          name: "EmailIndex",
          schema: "public",
          table: "AspNetUsers",
          column: "normalized_email");

      migrationBuilder.CreateIndex(
          name: "UserNameIndex",
          schema: "public",
          table: "AspNetUsers",
          column: "normalized_user_name",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_bill_items_bill_id",
          schema: "public",
          table: "BillItems",
          column: "bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_bills_legal_entity_address_country_id",
          schema: "public",
          table: "Bills",
          column: "legal_entity_address_country_id");

      migrationBuilder.CreateIndex(
          name: "ix_bills_number",
          schema: "public",
          table: "Bills",
          column: "number",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_bills_original_bill_id",
          schema: "public",
          table: "Bills",
          column: "original_bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_bills_payer_address_country_id",
          schema: "public",
          table: "Bills",
          column: "payer_address_country_id");

      migrationBuilder.CreateIndex(
          name: "ix_bills_reservation_id",
          schema: "public",
          table: "Bills",
          column: "reservation_id");

      migrationBuilder.CreateIndex(
          name: "ix_bills_scartation",
          schema: "public",
          table: "Bills",
          column: "scartation",
          filter: "\"scartation\" IS NOT NULL");

      migrationBuilder.CreateIndex(
          name: "ix_clean_infos_cleaning_plan_id_spot_id",
          schema: "public",
          table: "CleanInfos",
          columns: new[] { "cleaning_plan_id", "spot_id" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_clean_infos_spot_id",
          schema: "public",
          table: "CleanInfos",
          column: "spot_id");

      migrationBuilder.CreateIndex(
          name: "ix_cleaning_plans_date",
          schema: "public",
          table: "CleaningPlans",
          column: "date",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_event_spot_group_items_event_id_spot_group_id",
          schema: "public",
          table: "EventSpotGroupItems",
          columns: new[] { "event_id", "spot_group_id" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_event_spot_group_items_spot_group_id",
          schema: "public",
          table: "EventSpotGroupItems",
          column: "spot_group_id");

      migrationBuilder.CreateIndex(
          name: "ix_financial_closings_financial_closing_id",
          schema: "public",
          table: "FinancialClosings",
          column: "financial_closing_id",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_group_reservations_number",
          schema: "public",
          table: "GroupReservations",
          column: "number",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_group_reservation_spots_spot_id",
          schema: "public",
          table: "GroupReservationSpots",
          column: "spot_id");

      migrationBuilder.CreateIndex(
          name: "ix_guests_address_country_id",
          schema: "public",
          table: "Guests",
          column: "address_country_id");

      migrationBuilder.CreateIndex(
          name: "ix_guests_bill_id",
          schema: "public",
          table: "Guests",
          column: "bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_guests_nationality_id",
          schema: "public",
          table: "Guests",
          column: "nationality_id");

      migrationBuilder.CreateIndex(
          name: "ix_guests_reservation_id",
          schema: "public",
          table: "Guests",
          column: "reservation_id");

      migrationBuilder.CreateIndex(
          name: "ix_guests_scartation",
          schema: "public",
          table: "Guests",
          column: "scartation",
          filter: "\"scartation\" IS NOT NULL");

      migrationBuilder.CreateIndex(
          name: "ix_invoice_items_invoice_id",
          schema: "public",
          table: "InvoiceItems",
          column: "invoice_id");

      migrationBuilder.CreateIndex(
          name: "ix_invoices_legal_entity_address_country_id",
          schema: "public",
          table: "Invoices",
          column: "legal_entity_address_country_id");

      migrationBuilder.CreateIndex(
          name: "ix_invoices_linked_bill_id",
          schema: "public",
          table: "Invoices",
          column: "linked_bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_invoices_number",
          schema: "public",
          table: "Invoices",
          column: "number",
          unique: true,
          filter: "\"number\" IS NOT NULL");

      migrationBuilder.CreateIndex(
          name: "ix_invoices_payer_address_country_id",
          schema: "public",
          table: "Invoices",
          column: "payer_address_country_id");

      migrationBuilder.CreateIndex(
          name: "ix_invoices_reservation_id",
          schema: "public",
          table: "Invoices",
          column: "reservation_id");

      migrationBuilder.CreateIndex(
          name: "ix_invoices_scartation",
          schema: "public",
          table: "Invoices",
          column: "scartation",
          filter: "\"scartation\" IS NOT NULL");

      migrationBuilder.CreateIndex(
          name: "ix_languages_code",
          schema: "public",
          table: "Languages",
          column: "code",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_maintenance_issues_resolved_at_utc",
          schema: "public",
          table: "maintenance_issues",
          column: "resolved_at_utc");

      migrationBuilder.CreateIndex(
          name: "ix_maintenance_issues_solver_user_id",
          schema: "public",
          table: "maintenance_issues",
          column: "solver_user_id");

      migrationBuilder.CreateIndex(
          name: "ix_maintenance_issues_spot_id",
          schema: "public",
          table: "maintenance_issues",
          column: "spot_id");

      migrationBuilder.CreateIndex(
          name: "ix_nationalities_alpha2",
          schema: "public",
          table: "Nationalities",
          column: "alpha2",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_nationalities_alpha3",
          schema: "public",
          table: "Nationalities",
          column: "alpha3",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_nationalities_language_id",
          schema: "public",
          table: "Nationalities",
          column: "language_id");

      migrationBuilder.CreateIndex(
          name: "ix_nationalities_numeric",
          schema: "public",
          table: "Nationalities",
          column: "numeric",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_reservations_group_reservation_id",
          schema: "public",
          table: "Reservations",
          column: "group_reservation_id");

      migrationBuilder.CreateIndex(
          name: "ix_reservations_number",
          schema: "public",
          table: "Reservations",
          column: "number",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_reservations_secret",
          schema: "public",
          table: "Reservations",
          column: "secret");

      migrationBuilder.CreateIndex(
          name: "ix_reservation_service_items_reservation_id",
          schema: "public",
          table: "ReservationServiceItems",
          column: "reservation_id");

      migrationBuilder.CreateIndex(
          name: "ix_reservation_service_items_reservation_id_service_id",
          schema: "public",
          table: "ReservationServiceItems",
          columns: new[] { "reservation_id", "service_id" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_reservation_service_items_service_id",
          schema: "public",
          table: "ReservationServiceItems",
          column: "service_id");

      migrationBuilder.CreateIndex(
          name: "ix_reservation_spot_items_bill_id",
          schema: "public",
          table: "ReservationSpotItems",
          column: "bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_reservation_spot_items_reservation_id_spot_id",
          schema: "public",
          table: "ReservationSpotItems",
          columns: new[] { "reservation_id", "spot_id" });

      migrationBuilder.CreateIndex(
          name: "ix_reservation_spot_items_spot_group_id_reservation_id",
          schema: "public",
          table: "ReservationSpotItems",
          columns: new[] { "spot_group_id", "reservation_id" });

      migrationBuilder.CreateIndex(
          name: "ix_reservation_spot_items_spot_id",
          schema: "public",
          table: "ReservationSpotItems",
          column: "spot_id");

      migrationBuilder.CreateIndex(
          name: "ix_services_service_type_id",
          schema: "public",
          table: "Services",
          column: "service_type_id");

      migrationBuilder.CreateIndex(
          name: "ix_services_vat_rate_id",
          schema: "public",
          table: "Services",
          column: "vat_rate_id");

      migrationBuilder.CreateIndex(
          name: "ix_service_texts_language_id",
          schema: "public",
          table: "ServiceTexts",
          column: "language_id");

      migrationBuilder.CreateIndex(
          name: "ix_service_texts_service_id_language_id",
          schema: "public",
          table: "ServiceTexts",
          columns: new[] { "service_id", "language_id" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_spot_group_oof_items_out_of_order_id",
          schema: "public",
          table: "SpotGroupOofItems",
          column: "out_of_order_id");

      migrationBuilder.CreateIndex(
          name: "ix_spot_group_oof_items_spot_group_id",
          schema: "public",
          table: "SpotGroupOofItems",
          column: "spot_group_id");

      migrationBuilder.CreateIndex(
          name: "ix_spot_groups_service_id",
          schema: "public",
          table: "SpotGroups",
          column: "service_id");

      migrationBuilder.CreateIndex(
          name: "ix_spot_oof_items_out_of_order_id",
          schema: "public",
          table: "SpotOofItems",
          column: "out_of_order_id");

      migrationBuilder.CreateIndex(
          name: "ix_spot_oof_items_spot_id",
          schema: "public",
          table: "SpotOofItems",
          column: "spot_id");

      migrationBuilder.CreateIndex(
          name: "ix_spots_spot_group_id_name",
          schema: "public",
          table: "Spots",
          columns: new[] { "spot_group_id", "name" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_vehicles_bill_id",
          schema: "public",
          table: "Vehicles",
          column: "bill_id");

      migrationBuilder.CreateIndex(
          name: "ix_vehicles_reservation_id",
          schema: "public",
          table: "Vehicles",
          column: "reservation_id");

      migrationBuilder.CreateIndex(
          name: "ix_vehicles_service_id",
          schema: "public",
          table: "Vehicles",
          column: "service_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "AccessCards",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetRoleClaims",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetUserClaims",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetUserLogins",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetUserPasskeys",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetUserRoles",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetUserTokens",
          schema: "public");

      migrationBuilder.DropTable(
          name: "BillItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "BillNumberSequences",
          schema: "public");

      migrationBuilder.DropTable(
          name: "CleanInfos",
          schema: "public");

      migrationBuilder.DropTable(
          name: "EventSpotGroupItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "FinancialClosings",
          schema: "public");

      migrationBuilder.DropTable(
          name: "GroupReservationNumberSequences",
          schema: "public");

      migrationBuilder.DropTable(
          name: "GroupReservationSpots",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Guests",
          schema: "public");

      migrationBuilder.DropTable(
          name: "InvoiceItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "maintenance_issues",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Meals",
          schema: "public");

      migrationBuilder.DropTable(
          name: "ReservationNumberSequences",
          schema: "public");

      migrationBuilder.DropTable(
          name: "ReservationServiceItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "ReservationSpotItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "ServiceTexts",
          schema: "public");

      migrationBuilder.DropTable(
          name: "SpotGroupOofItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "SpotOofItems",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Vehicles",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetRoles",
          schema: "public");

      migrationBuilder.DropTable(
          name: "CleaningPlans",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Events",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Invoices",
          schema: "public");

      migrationBuilder.DropTable(
          name: "AspNetUsers",
          schema: "public");

      migrationBuilder.DropTable(
          name: "OutOfOrders",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Spots",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Bills",
          schema: "public");

      migrationBuilder.DropTable(
          name: "SpotGroups",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Nationalities",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Reservations",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Services",
          schema: "public");

      migrationBuilder.DropTable(
          name: "Languages",
          schema: "public");

      migrationBuilder.DropTable(
          name: "GroupReservations",
          schema: "public");

      migrationBuilder.DropTable(
          name: "ServiceTypes",
          schema: "public");

      migrationBuilder.DropTable(
          name: "VatRates",
          schema: "public");
    }
  }
}
