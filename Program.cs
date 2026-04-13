using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Newtonsoft.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURAÇÃO DE SERVIÇOS (CORS, INFRA) ---

// Configura o CORS para permitir que seu admin.html (local) e a Vercel acessem a API
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

app.MapGet("/ping", () => Results.Ok("pong"));

var app = builder.Build();

// --- 2. ATIVAÇÃO DE MIDDLEWARES ---

app.UseCors("PermissaoTotal");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

string supabaseUrl = "https://jaazylhdixbedgcfplng.supabase.co"; // Arrumado o HTTPS
string supabaseKey = "sb_publishable_TVbgb8x4kzf7_nxRu0VpTQ_7WUdnVrg"; // Verifique se é a anon public

var cloudAccount = new Account("dvff4c4oo", "846643659543355", "iUBT6n0yFbcHY5o-eYPqmDkz7Mo");
var cloudinary = new Cloudinary(cloudAccount);


// --- 3. ROTAS DA API (ENDPOINTS) ---

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

// Rota para o Admin (Lista tudo, inclusive escondidos)
app.MapGet("/imoveis/admin-lista", async () =>
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    var res = await client.GetAsync($"{supabaseUrl}/rest/v1/imoveis?select=*&order=id.desc");
    var json = await res.Content.ReadAsStringAsync();
    return Results.Content(json, "application/json");
});

// Rota para Salvar/Editar
app.MapPost("/imoveis/salvar-imovel", async (HttpRequest request) =>
{
    // Validação simples de API Key (Segurança)
    if (request.Headers["X-Api-Key"] != "MaricaImoveis2026@!") return Results.Unauthorized();

    var form = await request.ReadFormAsync();
    var id = int.Parse(form["idImovel"]);
    var titulo = form["titulo"].ToString();
    var preco = double.Parse(form["preco"]);
    var descricao = form["descricao"].ToString();
    var ativo = bool.Parse(form["ativo"]);
    string urlFoto = "";

    // Se enviou foto nova, sobe pro Cloudinary
    if (form.Files.Count > 0)
    {
        var file = form.Files[0];
        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "imoveis_amigo"
        };
        var uploadResult = await cloudinary.UploadAsync(uploadParams);
        urlFoto = uploadResult.SecureUrl.ToString();
    }

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");

    var dados = new { titulo, preco, descricao, ativo };
    var jsonBody = JsonConvert.SerializeObject(dados);
    
    // Se ID for 0, cria novo (POST). Se for > 0, atualiza (PATCH).
    HttpResponseMessage res;
    if (id == 0) {
        // Para novo, incluímos a foto obrigatoriamente
        var novoDados = new { titulo, preco, descricao, ativo, fotos = urlFoto };
        res = await client.PostAsync($"{supabaseUrl}/rest/v1/imoveis", 
            new StringContent(JsonConvert.SerializeObject(novoDados), Encoding.UTF8, "application/json"));
    } else {
        // Se não enviou foto nova na edição, não sobrescreve a antiga no banco
        string patchJson = urlFoto != "" 
            ? JsonConvert.SerializeObject(new { titulo, preco, descricao, ativo, fotos = urlFoto })
            : JsonConvert.SerializeObject(new { titulo, preco, descricao, ativo });
            
        res = await client.PatchAsync($"{supabaseUrl}/rest/v1/imoveis?id=eq.{id}", 
            new StringContent(patchJson, Encoding.UTF8, "application/json"));
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