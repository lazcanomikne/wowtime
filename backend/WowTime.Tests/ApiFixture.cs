using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WowTime.Api.Data;

namespace WowTime.Tests;

/// <summary>Levanta la API contra la base de pruebas (ConnectionStrings__Pruebas), la reinicia y carga los datos de demostración.</summary>
public class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        CargarEnv();
        var pruebas = Environment.GetEnvironmentVariable("ConnectionStrings__Pruebas")
            ?? throw new InvalidOperationException("Define ConnectionStrings__Pruebas (archivo backend/WowTime.Api/.env).");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", pruebas);
        Environment.SetEnvironmentVariable("Tareas__Deshabilitar", "true");
        Environment.SetEnvironmentVariable("Archivos__Ruta", Path.Combine(Path.GetTempPath(), "wowtime-pruebas-archivos"));
        Fabrica = new WebApplicationFactory<Program>();
        using var scope = Fabrica.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Semilla>().Cargar(true);
    }

    public Task DisposeAsync() { Fabrica.Dispose(); return Task.CompletedTask; }

    private static void CargarEnv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WowTime.Api", ".env"))) dir = dir.Parent;
        if (dir is null) return;
        foreach (var l in File.ReadAllLines(Path.Combine(dir.FullName, "WowTime.Api", ".env")))
        {
            var t = l.Trim(); var i = t.IndexOf('=');
            if (t.StartsWith('#') || i <= 0) continue;
            var k = t[..i]; if (Environment.GetEnvironmentVariable(k) is null || k == "ConnectionStrings__Pruebas") Environment.SetEnvironmentVariable(k, t[(i + 1)..].Trim('"'));
        }
    }

    public async Task<Cliente> Como(string email)
    {
        var http = Fabrica.CreateClient();
        var r = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Semilla.PasswordDemo });
        r.EnsureSuccessStatusCode();
        var j = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", j["tokenAcceso"]!.GetValue<string>());
        return new Cliente(http);
    }

    public T Servicio<T>(Func<IServiceProvider, T> f) { using var s = Fabrica.Services.CreateScope(); return f(s.ServiceProvider); }
}

public class Cliente(HttpClient http)
{
    public HttpClient Http => http;
    public async Task<(int status, JsonNode? cuerpo)> Get(string ruta) => await Leer(await http.GetAsync("/api/v1" + ruta));
    public async Task<(int status, JsonNode? cuerpo)> Post(string ruta, object? cuerpo = null) => await Leer(await http.PostAsJsonAsync("/api/v1" + ruta, cuerpo ?? new { }));
    public async Task<(int status, JsonNode? cuerpo)> Put(string ruta, object cuerpo) => await Leer(await http.PutAsJsonAsync("/api/v1" + ruta, cuerpo));
    private static async Task<(int, JsonNode?)> Leer(HttpResponseMessage r)
    {
        var t = await r.Content.ReadAsStringAsync();
        return ((int)r.StatusCode, string.IsNullOrWhiteSpace(t) ? null : JsonNode.Parse(t));
    }
    public async Task<JsonNode> Ok(Task<(int status, JsonNode? cuerpo)> t)
    {
        var (s, c) = await t;
        Assert.True(s is >= 200 and < 300, $"Se esperaba éxito y llegó {s}: {c?.ToJsonString()}");
        return c ?? new JsonObject();
    }
}

[CollectionDefinition("api")]
public class ColeccionApi : ICollectionFixture<ApiFixture>;
