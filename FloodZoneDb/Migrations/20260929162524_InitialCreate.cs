using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FloodZoneDb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:bank_side", "L,P")
                .Annotation("Npgsql:Enum:calc_status", "draft,computed,approved,archived")
                .Annotation("Npgsql:Enum:object_class", "I,II,III,IV")
                .Annotation("Npgsql:Enum:reliability_category", "особо ответственное,ответственное,пониженное");

            migrationBuilder.CreateTable(
                name: "dam_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dam_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generator_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generator_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "object_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    icon = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    inn = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    ogrn = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rivers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    length_km = table.Column<decimal>(type: "numeric", nullable: true),
                    basin_area_km2 = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "spillway_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spillway_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subjects_rf",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects_rf", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turbine_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turbine_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    object_type_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objects", x => x.id);
                    table.ForeignKey(
                        name: "fk_objects_object_types_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "object_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_objects_objects_parent_id",
                        column: x => x.parent_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "buildings",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric", nullable: true),
                    height_m = table.Column<decimal>(type: "numeric", nullable: true),
                    purpose = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildings", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_buildings_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calculations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    calc_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    performed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    performed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calculations", x => x.id);
                    table.ForeignKey(
                        name: "fk_calculations_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calculations_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "dams",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    dam_type_id = table.Column<int>(type: "integer", nullable: true),
                    length_m = table.Column<decimal>(type: "numeric", nullable: true),
                    max_height_m = table.Column<decimal>(type: "numeric", nullable: true),
                    normal_headwater_m = table.Column<decimal>(type: "numeric", nullable: true),
                    min_water_level_m = table.Column<decimal>(type: "numeric", nullable: true),
                    reservoir_volume_mln_m3 = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dams", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_dams_dam_types_dam_type_id",
                        column: x => x.dam_type_id,
                        principalTable: "dam_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_dams_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    equipment_type_id = table.Column<int>(type: "integer", nullable: true),
                    turbine_type_id = table.Column<int>(type: "integer", nullable: true),
                    generator_type_id = table.Column<int>(type: "integer", nullable: true),
                    voltage_kv = table.Column<decimal>(type: "numeric", nullable: true),
                    power_mw = table.Column<decimal>(type: "numeric", nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    installed_at = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_equipment_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_equipment_generator_types_generator_type_id",
                        column: x => x.generator_type_id,
                        principalTable: "generator_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_equipment_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_equipment_turbine_types_turbine_type_id",
                        column: x => x.turbine_type_id,
                        principalTable: "turbine_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "hydro_nodes",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    river_id = table.Column<int>(type: "integer", nullable: true),
                    purpose = table.Column<string>(type: "text", nullable: true),
                    commissioning_date = table.Column<DateOnly>(type: "date", nullable: true),
                    owner_org_id = table.Column<int>(type: "integer", nullable: true),
                    balance_affiliation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    responsible_person = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status_id = table.Column<int>(type: "integer", nullable: true),
                    gts_class = table.Column<int>(type: "integer", nullable: true),
                    reliability_category = table.Column<int>(type: "integer", nullable: true),
                    design_org_id = table.Column<int>(type: "integer", nullable: true),
                    project_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    design_power_mw = table.Column<decimal>(type: "numeric", nullable: true),
                    units_count = table.Column<int>(type: "integer", nullable: true),
                    avg_annual_generation_mkwh = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hydro_nodes", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_hydro_nodes_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hydro_nodes_organizations_design_org_id",
                        column: x => x.design_org_id,
                        principalTable: "organizations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_hydro_nodes_organizations_owner_org_id",
                        column: x => x.owner_org_id,
                        principalTable: "organizations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_hydro_nodes_rivers_river_id",
                        column: x => x.river_id,
                        principalTable: "rivers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_hydro_nodes_statuses_status_id",
                        column: x => x.status_id,
                        principalTable: "statuses",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "object_condition",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    physical_wear_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    tech_condition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_inspection_at = table.Column<DateOnly>(type: "date", nullable: true),
                    next_inspection_at = table.Column<DateOnly>(type: "date", nullable: true),
                    accident_rate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    special_marks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_condition", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_object_condition_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "object_location",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    subject_rf_id = table.Column<int>(type: "integer", nullable: true),
                    district = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_location", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_object_location_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_object_location_subjects_rf_subject_rf_id",
                        column: x => x.subject_rf_id,
                        principalTable: "subjects_rf",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "object_media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_object_media_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "object_notes",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    short_description = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_notes", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_object_notes_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "object_quick_info",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_quick_info", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_object_quick_info_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_object_quick_info_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "object_technical_specs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    spec_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    spec_value = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_technical_specs", x => x.id);
                    table.ForeignKey(
                        name: "fk_object_technical_specs_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spillways",
                columns: table => new
                {
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    spillway_type_id = table.Column<int>(type: "integer", nullable: true),
                    max_capacity_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    openings_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spillways", x => x.object_id);
                    table.ForeignKey(
                        name: "fk_spillways_objects_object_id",
                        column: x => x.object_id,
                        principalTable: "objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_spillways_spillway_types_spillway_type_id",
                        column: x => x.spillway_type_id,
                        principalTable: "spillway_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "calc_flow_provision",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    provision_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    flow_m3s = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_flow_provision", x => x.id);
                    table.ForeignKey(
                        name: "fk_calc_flow_provision_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_hydrological",
                columns: table => new
                {
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    catchment_area_km2 = table.Column<decimal>(type: "numeric", nullable: true),
                    avg_annual_runoff_km3 = table.Column<decimal>(type: "numeric", nullable: true),
                    max_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    min_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    provision_pct = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_hydrological", x => x.calculation_id);
                    table.ForeignKey(
                        name: "fk_calc_hydrological_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_initial",
                columns: table => new
                {
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    normal_headwater_m = table.Column<decimal>(type: "numeric", nullable: true),
                    dead_volume_level_m = table.Column<decimal>(type: "numeric", nullable: true),
                    reservoir_full_volume_mln_m3 = table.Column<decimal>(type: "numeric", nullable: true),
                    reservoir_useful_volume_mln_m3 = table.Column<decimal>(type: "numeric", nullable: true),
                    roughness_coeff_n = table.Column<decimal>(type: "numeric", nullable: true),
                    water_temperature_c = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_initial", x => x.calculation_id);
                    table.ForeignKey(
                        name: "fk_calc_initial_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_main_results",
                columns: table => new
                {
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    throughput_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    max_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    min_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    avg_velocity_ms = table.Column<decimal>(type: "numeric", nullable: true),
                    upstream_level_m = table.Column<decimal>(type: "numeric", nullable: true),
                    downstream_level_m = table.Column<decimal>(type: "numeric", nullable: true),
                    efficiency_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    annual_generation_mkwh = table.Column<decimal>(type: "numeric", nullable: true),
                    reliability_coeff = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_main_results", x => x.calculation_id);
                    table.ForeignKey(
                        name: "fk_calc_main_results_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    caption = table.Column<string>(type: "text", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_calc_media_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_operation_modes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    period = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avg_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    max_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    min_flow_m3s = table.Column<decimal>(type: "numeric", nullable: true),
                    upstream_level_m = table.Column<decimal>(type: "numeric", nullable: true),
                    downstream_level_m = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_operation_modes", x => x.id);
                    table.ForeignKey(
                        name: "fk_calc_operation_modes_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_power_generation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    month_num = table.Column<short>(type: "smallint", nullable: false),
                    generation_mkwh = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_power_generation", x => x.id);
                    table.ForeignKey(
                        name: "fk_calc_power_generation_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calc_structural",
                columns: table => new
                {
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    dam_type_id = table.Column<int>(type: "integer", nullable: true),
                    dam_length_m = table.Column<decimal>(type: "numeric", nullable: true),
                    dam_max_height_m = table.Column<decimal>(type: "numeric", nullable: true),
                    crest_width_m = table.Column<decimal>(type: "numeric", nullable: true),
                    spillways_count = table.Column<int>(type: "integer", nullable: true),
                    spillway_type_id = table.Column<int>(type: "integer", nullable: true),
                    turbine_type_id = table.Column<int>(type: "integer", nullable: true),
                    units_count = table.Column<int>(type: "integer", nullable: true),
                    installed_power_mw = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_structural", x => x.calculation_id);
                    table.ForeignKey(
                        name: "fk_calc_structural_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calc_structural_dam_types_dam_type_id",
                        column: x => x.dam_type_id,
                        principalTable: "dam_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_calc_structural_spillway_types_spillway_type_id",
                        column: x => x.spillway_type_id,
                        principalTable: "spillway_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_calc_structural_turbine_types_turbine_type_id",
                        column: x => x.turbine_type_id,
                        principalTable: "turbine_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "calc_water_balance",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    calculation_id = table.Column<long>(type: "bigint", nullable: false),
                    use_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    share_pct = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calc_water_balance", x => x.id);
                    table.ForeignKey(
                        name: "fk_calc_water_balance_calculations_calculation_id",
                        column: x => x.calculation_id,
                        principalTable: "calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calc_flow_provision_calculation_id",
                table: "calc_flow_provision",
                column: "calculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_media_calculation_id",
                table: "calc_media",
                column: "calculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_operation_modes_calculation_id",
                table: "calc_operation_modes",
                column: "calculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_power_generation_calculation_id",
                table: "calc_power_generation",
                column: "calculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_structural_dam_type_id",
                table: "calc_structural",
                column: "dam_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_structural_spillway_type_id",
                table: "calc_structural",
                column: "spillway_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_structural_turbine_type_id",
                table: "calc_structural",
                column: "turbine_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_calc_water_balance_calculation_id",
                table: "calc_water_balance",
                column: "calculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_calculations_object_id_calc_number",
                table: "calculations",
                columns: new[] { "object_id", "calc_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calculations_performed_by_user_id",
                table: "calculations",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dams_dam_type_id",
                table: "dams",
                column: "dam_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_equipment_type_id",
                table: "equipment",
                column: "equipment_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_generator_type_id",
                table: "equipment",
                column: "generator_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_turbine_type_id",
                table: "equipment",
                column: "turbine_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_types_code",
                table: "equipment_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hydro_nodes_design_org_id",
                table: "hydro_nodes",
                column: "design_org_id");

            migrationBuilder.CreateIndex(
                name: "ix_hydro_nodes_owner_org_id",
                table: "hydro_nodes",
                column: "owner_org_id");

            migrationBuilder.CreateIndex(
                name: "ix_hydro_nodes_river_id",
                table: "hydro_nodes",
                column: "river_id");

            migrationBuilder.CreateIndex(
                name: "ix_hydro_nodes_status_id",
                table: "hydro_nodes",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_object_location_subject_rf_id",
                table: "object_location",
                column: "subject_rf_id");

            migrationBuilder.CreateIndex(
                name: "ix_object_media_object_id",
                table: "object_media",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ix_object_quick_info_updated_by_user_id",
                table: "object_quick_info",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_object_technical_specs_object_id",
                table: "object_technical_specs",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ix_object_types_code",
                table: "object_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_objects_object_type_id",
                table: "objects",
                column: "object_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_objects_parent_id",
                table: "objects",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_inn",
                table: "organizations",
                column: "inn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rivers_name",
                table: "rivers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_spillways_spillway_type_id",
                table: "spillways",
                column: "spillway_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_statuses_code",
                table: "statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subjects_rf_name",
                table: "subjects_rf",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_login",
                table: "users",
                column: "login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buildings");

            migrationBuilder.DropTable(
                name: "calc_flow_provision");

            migrationBuilder.DropTable(
                name: "calc_hydrological");

            migrationBuilder.DropTable(
                name: "calc_initial");

            migrationBuilder.DropTable(
                name: "calc_main_results");

            migrationBuilder.DropTable(
                name: "calc_media");

            migrationBuilder.DropTable(
                name: "calc_operation_modes");

            migrationBuilder.DropTable(
                name: "calc_power_generation");

            migrationBuilder.DropTable(
                name: "calc_structural");

            migrationBuilder.DropTable(
                name: "calc_water_balance");

            migrationBuilder.DropTable(
                name: "dams");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "hydro_nodes");

            migrationBuilder.DropTable(
                name: "object_condition");

            migrationBuilder.DropTable(
                name: "object_location");

            migrationBuilder.DropTable(
                name: "object_media");

            migrationBuilder.DropTable(
                name: "object_notes");

            migrationBuilder.DropTable(
                name: "object_quick_info");

            migrationBuilder.DropTable(
                name: "object_technical_specs");

            migrationBuilder.DropTable(
                name: "spillways");

            migrationBuilder.DropTable(
                name: "calculations");

            migrationBuilder.DropTable(
                name: "dam_types");

            migrationBuilder.DropTable(
                name: "equipment_types");

            migrationBuilder.DropTable(
                name: "generator_types");

            migrationBuilder.DropTable(
                name: "turbine_types");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "rivers");

            migrationBuilder.DropTable(
                name: "statuses");

            migrationBuilder.DropTable(
                name: "subjects_rf");

            migrationBuilder.DropTable(
                name: "spillway_types");

            migrationBuilder.DropTable(
                name: "objects");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "object_types");
        }
    }
}
