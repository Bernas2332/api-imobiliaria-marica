using Postgrest.Attributes;
using Postgrest.Models;

namespace SiteImobiliaria.Models
{
    [Table("imoveis")]
    public class Imovel : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("titulo")]
        public string? Titulo { get; set; }

        [Column("descricao")]
        public string? Descricao { get; set; }

        [Column("preco")]
        public decimal? Preco { get; set; }

        [Column("fotos")]
        public string? Fotos { get; set; }

        [Column("ativo")]
        public bool Ativo { get; set; } = true;
    }
}