using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURAÇÃO DE LIMITES DE UPLOAD (100 MB) ---
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; 
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; 
});

// --- 2. CONFIGURAÇÃO DE SERVIÇOS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermissaoTotal", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- 3. ATIVAÇÃO DE MIDDLEWARES ---
app.UseCors("PermissaoTotal");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

string supabaseUrl = "https://jaazylhdixbedgcfplng.supabase.co";
string supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? "";

// Lembre-se: Para a construtora, isso aqui também deve virar Variável de Ambiente!
var cloudAccount = new Account("dvff4c4oo", "846643659543355", "iUBT6n0yFbcHY5o-eYPqmDkz7Mo");
var cloudinary = new Cloudinary(cloudAccount);

// --- 4. ROTAS DA API ---

app.MapGet("/ping", () => Results.Ok("pong"));

app.MapGet("/imoveis", async () =>
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    // Ordenação: 1º Prioridade (ascendente: 1, 2, 3), 2º ID (descendente: mais novo primeiro)
    var res = await client.GetAsync($"{supabaseUrl}/rest/v1/imoveis?select=*&ativo=eq.true&order=prioridade.asc,id.desc");
    var json = await res.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
});

app.MapGet("/imoveis/admin-lista", async () =>
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    // Ordenação mantida para o painel de admin também
    var res = await client.GetAsync($"{supabaseUrl}/rest/v1/imoveis?select=*&order=prioridade.asc,id.desc");
    var json = await res.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
});

app.MapPost("/imoveis/salvar-imovel", async (HttpRequest request) =>
{
    if (request.Headers["X-Api-Key"] != "MaricaImoveis2026@!") return Results.Unauthorized();

    var form = await request.ReadFormAsync();
    var id = int.Parse(form["idImovel"]);
    var titulo = form["titulo"].ToString();
    var tipo = form["tipo"].ToString().ToLower();
    
    var precoStr = form["preco"].ToString().Replace(",", ".");
    var preco = double.Parse(precoStr, System.Globalization.CultureInfo.InvariantCulture);
    
    var descricao = form["descricao"].ToString();
    var ativo = bool.Parse(form["ativo"]);
    
    // Capturando a prioridade (se vier vazio, assume 2 como padrão normal)
    int prioridade = 2;
    if (!string.IsNullOrEmpty(form["prioridade"]))
    {
        prioridade = int.Parse(form["prioridade"]);
    }
    
    List<string> linksSubidos = new List<string>();

    if (form.Files.Count > 0)
    {
        foreach (var file in form.Files)
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "imoveis_amigo"
            };
            var uploadResult = await cloudinary.UploadAsync(uploadParams);
            linksSubidos.Add(uploadResult.SecureUrl.ToString());
        }
    }

    string urlFotosFinal = string.Join(",", linksSubidos);

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    HttpResponseMessage res;
    
    if (id == 0) 
    {
        // Adicionado o campo prioridade no envio novo
        var novoDados = new { titulo, tipo, preco, descricao, ativo, prioridade, fotos = urlFotosFinal };
        res = await client.PostAsync($"{supabaseUrl}/rest/v1/imoveis", 
            new StringContent(JsonConvert.SerializeObject(novoDados), Encoding.UTF8, "application/json"));
    } 
    else 
    {
        object dadosUpdate;
        if (!string.IsNullOrEmpty(urlFotosFinal)) {
            // Adicionado o campo prioridade na atualização
            dadosUpdate = new { titulo, tipo, preco, descricao, ativo, prioridade, fotos = urlFotosFinal };
        } else {
            dadosUpdate = new { titulo, tipo, preco, descricao, ativo, prioridade };
        }

        res = await client.PatchAsync($"{supabaseUrl}/rest/v1/imoveis?id=eq.{id}", 
            new StringContent(JsonConvert.SerializeObject(dadosUpdate), Encoding.UTF8, "application/json"));
    }

    if (res.IsSuccessStatusCode) 
    {
        return Results.Ok();
    } 
    else 
    {
        var erroSupabase = await res.Content.ReadAsStringAsync();
        Console.WriteLine("\n=== ERRO NO SUPABASE ===");
        Console.WriteLine(erroSupabase);
        Console.WriteLine("========================\n");
        return Results.BadRequest(erroSupabase);
    }
});

app.MapDelete("/imoveis/deletar-imovel/{id}", async (int id, HttpRequest request) =>
{
    if (request.Headers["X-Api-Key"] != "MaricaImoveis2026@!") return Results.Unauthorized();

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    var res = await client.DeleteAsync($"{supabaseUrl}/rest/v1/imoveis?id=eq.{id}");
    return res.IsSuccessStatusCode ? Results.Ok() : Results.BadRequest();
});

app.Run();