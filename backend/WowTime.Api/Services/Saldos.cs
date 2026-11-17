using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services;

public record ResumenSaldo(decimal Disponibles, decimal Comprometidos, decimal Usados, decimal Otorgados,
    decimal PorVencer, DateOnly? FechaLimiteProxima, decimal DiasProximoVencimiento, DateOnly? FechaProximoVencimiento);

/// <summary>Saldos de vacaciones: otorgamiento, compromiso, consumo, liberación y vencimiento.</summary>
public class ServicioSaldos(AppDb db, IReloj reloj)
{
    public static IQueryable<SaldoVacaciones> Vigentes(IQueryable<SaldoVacaciones> q, DateOnly hoy)
        => q.Where(s => s.FechaOtorgamiento <= hoy && s.FechaLimite >= hoy);

    public async Task<ResumenSaldo> Resumen(int colaboradorId, DateOnly hoy, int diasAlerta = 30)
    {
        var saldos = await Vigentes(db.Saldos.Where(s => s.ColaboradorId == colaboradorId), hoy).OrderBy(s => s.FechaLimite).ToListAsync();
        return Calcular(saldos, hoy, diasAlerta);
    }

    public static ResumenSaldo Calcular(List<SaldoVacaciones> saldos, DateOnly hoy, int diasAlerta)
    {
        var disp = saldos.Sum(s => s.Disponibles);
        var limite = hoy.AddDays(diasAlerta);
        var porVencer = saldos.Where(s => s.FechaLimite <= limite).Sum(s => Math.Max(0, s.Disponibles));
        var prox = saldos.Where(s => s.Disponibles > 0).OrderBy(s => s.FechaLimite).FirstOrDefault();
        return new ResumenSaldo(disp, saldos.Sum(s => s.DiasComprometidos), saldos.Sum(s => s.DiasUsados), saldos.Sum(s => s.DiasOtorgados),
            porVencer, saldos.Where(s => s.FechaLimite <= limite && s.Disponibles > 0).Select(s => (DateOnly?)s.FechaLimite).Min(),
            prox?.Disponibles ?? 0, prox?.FechaLimite);
    }

    /// <summary>Compromete días al enviar la solicitud. Consume primero los saldos que vencen antes.</summary>
    public async Task Comprometer(Solicitud s, DateOnly hoy, int? usuarioId)
    {
        var saldos = await Vigentes(db.Saldos.Where(x => x.ColaboradorId == s.ColaboradorId), hoy).OrderBy(x => x.FechaLimite).ToListAsync();
        var falta = s.DiasHabiles;
        if (saldos.Sum(x => x.Disponibles) < falta)
            throw ApiException.Validacion("No tienes saldo suficiente para esta solicitud.");
        foreach (var saldo in saldos)
        {
            if (falta <= 0) break;
            var toma = Math.Min(saldo.Disponibles, falta);
            if (toma <= 0) continue;
            saldo.DiasComprometidos += toma;
            falta -= toma;
            db.MovimientosSaldo.Add(new MovimientoSaldo
            {
                SaldoVacacionesId = saldo.Id, ColaboradorId = s.ColaboradorId, Tipo = TipoMovimientoSaldo.Compromiso, Dias = toma,
                SolicitudId = s.Id, Fecha = reloj.Ahora, UsuarioId = usuarioId, Motivo = $"Solicitud {s.Folio}"
            });
        }
    }

    private async Task<Dictionary<int, decimal>> ComprometidoPorSaldo(int solicitudId)
    {
        var movs = await db.MovimientosSaldo.Where(m => m.SolicitudId == solicitudId).ToListAsync();
        movs.AddRange(db.ChangeTracker.Entries<MovimientoSaldo>().Where(e => e.State == EntityState.Added && e.Entity.SolicitudId == solicitudId).Select(e => e.Entity));
        return movs.GroupBy(m => m.SaldoVacacionesId).ToDictionary(g => g.Key, g => g.Sum(m => m.Tipo switch
        {
            TipoMovimientoSaldo.Compromiso => m.Dias,
            TipoMovimientoSaldo.Liberacion or TipoMovimientoSaldo.Consumo => -m.Dias,
            _ => 0
        }));
    }

    /// <summary>Al aprobar: los días comprometidos pasan a usados.</summary>
    public Task Consumir(Solicitud s, int? usuarioId) => Mover(s, usuarioId, TipoMovimientoSaldo.Consumo);

    /// <summary>Al rechazar o cancelar: los días comprometidos se liberan.</summary>
    public Task Liberar(Solicitud s, int? usuarioId) => Mover(s, usuarioId, TipoMovimientoSaldo.Liberacion);

    private async Task Mover(Solicitud s, int? usuarioId, TipoMovimientoSaldo tipo)
    {
        var porSaldo = await ComprometidoPorSaldo(s.Id);
        foreach (var (saldoId, dias) in porSaldo.Where(kv => kv.Value > 0))
        {
            var saldo = await db.Saldos.FindAsync(saldoId);
            if (saldo is null) continue;
            saldo.DiasComprometidos -= dias;
            if (tipo == TipoMovimientoSaldo.Consumo) saldo.DiasUsados += dias;
            db.MovimientosSaldo.Add(new MovimientoSaldo
            {
                SaldoVacacionesId = saldoId, ColaboradorId = s.ColaboradorId, Tipo = tipo, Dias = dias, SolicitudId = s.Id,
                Fecha = reloj.Ahora, UsuarioId = usuarioId, Motivo = $"Solicitud {s.Folio}"
            });
        }
    }

    /// <summary>Otorga los periodos cumplidos que falten. Idempotente.</summary>
    public async Task<int> Otorgar(Colaborador c, PoliticaVacaciones pol, DateOnly hoy)
    {
        var existentes = await db.Saldos.Where(s => s.ColaboradorId == c.Id).Select(s => s.Periodo).ToListAsync();
        var n = 0;
        for (var anio = 1; ; anio++)
        {
            var fecha = c.FechaIngreso.AddYears(anio);
            if (fecha > hoy) break;
            var limite = fecha.AddMonths(pol.PlazoVencimientoMeses);
            if (limite < hoy || existentes.Contains(anio)) continue; // los periodos ya vencidos no se otorgan retroactivamente
            var dias = Reglas.DiasPorAntiguedad(anio, pol.Tabla);
            var saldo = new SaldoVacaciones
            {
                ColaboradorId = c.Id, Periodo = anio, FechaOtorgamiento = fecha, FechaLimite = limite, DiasOtorgados = dias
            };
            db.Saldos.Add(saldo);
            await db.SaveChangesAsync();
            db.MovimientosSaldo.Add(new MovimientoSaldo
            {
                SaldoVacacionesId = saldo.Id, ColaboradorId = c.Id, Tipo = TipoMovimientoSaldo.Otorgamiento, Dias = dias,
                Fecha = reloj.Ahora, Motivo = $"Periodo {anio} ({pol.Nombre})"
            });
            n++;
        }
        return n;
    }

    /// <summary>Marca como vencidos los días disponibles de saldos cuya fecha límite ya pasó. Idempotente.</summary>
    public async Task<int> Vencer(DateOnly hoy)
    {
        var vencidos = await db.Saldos.Where(s => s.FechaLimite < hoy && s.DiasOtorgados - s.DiasUsados - s.DiasComprometidos - s.DiasVencidos > 0).ToListAsync();
        foreach (var s in vencidos)
        {
            var d = s.Disponibles;
            s.DiasVencidos += d;
            db.MovimientosSaldo.Add(new MovimientoSaldo
            {
                SaldoVacacionesId = s.Id, ColaboradorId = s.ColaboradorId, Tipo = TipoMovimientoSaldo.Vencimiento, Dias = d,
                Fecha = reloj.Ahora, Motivo = $"Vencimiento del periodo {s.Periodo}"
            });
        }
        return vencidos.Count;
    }
}
