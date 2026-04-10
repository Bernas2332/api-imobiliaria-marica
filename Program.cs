using Supabase;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => {
    options.AddPolicy("LiberarGeral", policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "";
var supabaseKey = builder.Configuration["Supabase:Key"] ?? "";
builder.Services.AddScoped<Supabase.Client>(_ => 
    new Supabase.Client(supabaseUrl, supabaseKey, new SupabaseOptions { AutoConnectRealtime = true })
);

var cloudinaryAccount = new Account(
    builder.Configuration["Cloudinary:CloudName"],
    builder.Configuration["Cloudinary:ApiKey"],
    builder.Configuration["Cloudinary:ApiSecret"]
);
builder.Services.AddSingleton(new Cloudinary(cloudinaryAccount));

var app = builder.Build();
app.UseCors("LiberarGeral");

const string MINHA_CHAVE_MESTRA = "MaricaImoveis2026@!"; 

// 1. VITRINE
app.MapGet("/imoveis", async (Supabase.Client client) => {
    try {
        var response = await client.From<SiteImobiliaria.Models.Imovel>().Where(x => x.Ativo == true).Get();
        var lista = response.Models.Select(x => new {
            id = x.Id,
            titulo = x.Titulo ?? "Sem título",
            descricao = x.Descricao ?? "",
            preco = x.Preco ?? 0, // Essencial para o filtro no Front-end
            precoFormatado = x.Preco.HasValue ? x.Preco.Value.ToString("N2") : "Sob Consulta",
            fotos = x.Fotos
        }).OrderByDescending(x => x.id).ToList();
        return Results.Ok(lista);
    } catch (Exception ex) { 
        Console.WriteLine($"Erro Vitrine: {ex.Message}");
        return Results.Problem("Erro ao carregar vitrine."); 
    }
});

// 2. ADMIN LISTA
app.MapGet("/admin-lista", async (Supabase.Client client) => {
    try {
        var response = await client.From<SiteImobiliaria.Models.Imovel>().Get();
        var lista = response.Models.Select(x => new {
            id = x.Id,
            titulo = x.Titulo ?? "Imóvel Sem Título",
            descricao = x.Descricao ?? "",
            preco = x.Preco ?? 0,
            ativo = x.Ativo
        }).OrderByDescending(x => x.id).ToList();
        return Results.Ok(lista);
    } catch (Exception ex) { 
        Console.WriteLine($"Erro Admin-Lista: {ex.Message}");
        return Results.Json(new { erro = ex.Message }, statusCode: 500); 
    }
});

// 3. SALVAR / EDITAR
app.MapPost("/salvar-imovel", async (
    [FromHeader(Name = "X-Api-Key")] string apiKey, 
    [FromForm] IFormFile? arquivo, 
    [FromForm] int idImovel, 
    [FromForm] string titulo,
    [FromForm] string descricao,
    [FromForm] decimal preco,
    [FromForm] bool ativo, 
    Cloudinary cloudinary, 
    Supabase.Client client) => 
{
    if (apiKey != MINHA_CHAVE_MESTRA) return Results.Unauthorized();
    try {
        string? urlGerada = null;
        if (arquivo != null && arquivo.Length > 0) {
            using var stream = arquivo.OpenReadStream();
            var uploadParams = new ImageUploadParams() { File = new FileDescription(arquivo.FileName, stream), Folder = "imoveis_amigo" };
            var result = await cloudinary.UploadAsync(uploadParams);
            urlGerada = result.SecureUrl.ToString();
        }

        if (idImovel > 0) {
            var query = client.From<SiteImobiliaria.Models.Imovel>().Where(x => x.Id == idImovel)
                .Set(x => x.Titulo, titulo).Set(x => x.Descricao, descricao).Set(x => x.Preco, preco).Set(x => x.Ativo, ativo);
            if (urlGerada != null) query = query.Set(x => x.Fotos, urlGerada);
            await query.Update();
        } else {
            var novo = new SiteImobiliaria.Models.Imovel { Titulo = titulo, Descricao = descricao, Preco = preco, Fotos = urlGerada, Ativo = true };
            await client.From<SiteImobiliaria.Models.Imovel>().Insert(novo);
        }
        return Results.Ok(new { mensagem = "Sucesso!" });
    } catch (Exception ex) { 
        Console.WriteLine($"Erro Salvar: {ex.Message}");
        return Results.Problem(ex.Message); 
    }
}).DisableAntiforgery();

// 4. DELETAR
app.MapDelete("/deletar-imovel/{id}", async (int id, [FromHeader(Name = "X-Api-Key")] string apiKey, Supabase.Client client) => {
    if (apiKey != MINHA_CHAVE_MESTRA) return Results.Unauthorized();
    try {
        await client.From<SiteImobiliaria.Models.Imovel>().Where(x => x.Id == id).Delete();
        return Results.Ok();
    } catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.Run();