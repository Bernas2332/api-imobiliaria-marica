using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Newtonsoft.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURAÇÃO DE SERVIÇOS ---

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

// --- 2. ATIVAÇÃO DE MIDDLEWARES ---

app.UseCors("PermissaoTotal");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configurações Supabase e Cloudinary
// Dica de CC: No futuro, coloque essas chaves no appsettings.json ou em variáveis de ambiente!
string supabaseUrl = "https://jaazylhdixbedgcfplng.supabase.co";
string supabaseKey = "sb_publishable_TVbgb8x4kzf7_nxRu0VpTQ_7WUdnVrg";

var cloudAccount = new Account("dvff4c4oo", "846643659543355", "iUBT6n0yFbcHY5o-eYPqmDkz7Mo");
var cloudinary = new Cloudinary(cloudAccount);


// --- 3. ROTAS DA API (ENDPOINTS) ---

// ROTA DE SAÚDE (Para o Cron-job não dar erro de tamanho)
app.MapGet("/ping", () => Results.Ok("pong"));

// Rota para a Vitrine (Apenas ativos)
app.MapGet("/imoveis", async () =>
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    var res = await client.GetAsync($"{supabaseUrl}/rest/v1/imoveis?select=*&ativo=eq.true&order=id.desc");
    var json = await res.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
});

// Rota para o Admin (Lista tudo)
app.MapGet("/imoveis/admin-lista", async () =>
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    var res = await client.GetAsync($"{supabaseUrl}/rest/v1/imoveis?select=*&order=id.desc");
    var json = await res.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
});

// Rota para Salvar/Editar (Múltiplas Fotos + Tipo)
app.MapPost("/imoveis/salvar-imovel", async (HttpRequest request) =>
{
    if (request.Headers["X-Api-Key"] != "MaricaImoveis2026@!") return Results.Unauthorized();

    var form = await request.ReadFormAsync();
    var id = int.Parse(form["idImovel"]);
    var titulo = form["titulo"].ToString();
    
    // CAPTURA DO NOVO CAMPO
    var tipo = form["tipo"].ToString().ToLower(); 
    
    var preco = double.Parse(form["preco"]);
    var descricao = form["descricao"].ToString();
    var ativo = bool.Parse(form["ativo"]);
    
    // Lista para acumular links do Cloudinary
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

    // Une os links em string separada por vírgula
    string urlFotosFinal = string.Join(",", linksSubidos);

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    HttpResponseMessage res;
    
    if (id == 0) 
    {
        // NOVO IMÓVEL: Inclui o 'tipo' no JSON
        var novoDados = new { titulo, tipo, preco, descricao, ativo, fotos = urlFotosFinal };
        res = await client.PostAsync($"{supabaseUrl}/rest/v1/imoveis", 
            new StringContent(JsonConvert.SerializeObject(novoDados), Encoding.UTF8, "application/json"));
    } 
    else 
    {
        // EDITAR IMÓVEL: Inclui o 'tipo' no JSON
        object dadosUpdate;
        if (!string.IsNullOrEmpty(urlFotosFinal)) {
            dadosUpdate = new { titulo, tipo, preco, descricao, ativo, fotos = urlFotosFinal };
        } else {
            dadosUpdate = new { titulo, tipo, preco, descricao, ativo };
        }

        res = await client.PatchAsync($"{supabaseUrl}/rest/v1/imoveis?id=eq.{id}", 
            new StringContent(JsonConvert.SerializeObject(dadosUpdate), Encoding.UTF8, "application/json"));
    }

    return res.IsSuccessStatusCode ? Results.Ok() : Results.BadRequest();
});

// Rota para Deletar
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