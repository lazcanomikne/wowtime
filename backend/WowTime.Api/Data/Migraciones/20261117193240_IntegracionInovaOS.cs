using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WowTime.Api.Data.Migraciones
{
    /// <inheritdoc />
    public partial class IntegracionInovaOS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Clave",
                table: "TiposPermiso",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiasDescuento",
                table: "TiposPermiso",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Parcial",
                table: "TiposPermiso",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Canal",
                table: "Solicitudes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Descuento",
                table: "Solicitudes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigenFirma",
                table: "Solicitudes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigenId",
                table: "Solicitudes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notas",
                table: "Saldos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigenFirma",
                table: "Saldos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigenId",
                table: "Saldos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrigenId",
                table: "Registros",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigenFirma",
                table: "Colaboradores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigenId",
                table: "Colaboradores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Canal",
                table: "Bitacora",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposPermiso_Clave",
                table: "TiposPermiso",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_OrigenId",
                table: "Solicitudes",
                column: "OrigenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Saldos_OrigenId",
                table: "Saldos",
                column: "OrigenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registros_OrigenId",
                table: "Registros",
                column: "OrigenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_OrigenId",
                table: "Colaboradores",
                column: "OrigenId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TiposPermiso_Clave",
                table: "TiposPermiso");

            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_OrigenId",
                table: "Solicitudes");

            migrationBuilder.DropIndex(
                name: "IX_Saldos_OrigenId",
                table: "Saldos");

            migrationBuilder.DropIndex(
                name: "IX_Registros_OrigenId",
                table: "Registros");

            migrationBuilder.DropIndex(
                name: "IX_Colaboradores_OrigenId",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "Clave",
                table: "TiposPermiso");

            migrationBuilder.DropColumn(
                name: "DiasDescuento",
                table: "TiposPermiso");

            migrationBuilder.DropColumn(
                name: "Parcial",
                table: "TiposPermiso");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "Descuento",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "OrigenFirma",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "OrigenId",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "Notas",
                table: "Saldos");

            migrationBuilder.DropColumn(
                name: "OrigenFirma",
                table: "Saldos");

            migrationBuilder.DropColumn(
                name: "OrigenId",
                table: "Saldos");

            migrationBuilder.DropColumn(
                name: "OrigenId",
                table: "Registros");

            migrationBuilder.DropColumn(
                name: "OrigenFirma",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "OrigenId",
                table: "Colaboradores");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "Bitacora");
        }
    }
}
