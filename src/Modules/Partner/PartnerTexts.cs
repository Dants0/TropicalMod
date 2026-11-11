using System;

namespace TropicalMod.Modules.Partner
{
    /// <summary>Falas do parceiro (legendas). Mais uma lista boa pra pedir sugestão nos comentários.</summary>
    public static class PartnerTexts
    {
        public static readonly string[] FemaleNames = { "Rosa", "Jessica", "Dandara", "Bia", "Lorena", "Kelly" };
        public static readonly string[] MaleNames = { "Tonhao", "Marcao", "Juninho", "Wesley", "Careca", "Batata" };

        public static readonly string[] Recruited =
        {
            "Fechou. Mas se der ruim, eu nao te conheco.",
            "Bora. Metade de tudo e minha, hein.",
            "Finalmente alguem com coragem nessa cidade.",
        };

        public static readonly string[] Dismissed =
        {
            "Valeu pela parceria. Ou nao.",
            "Ta bom, ta bom. Me liga.",
        };

        public static readonly string[] WantedUp =
        {
            "SUJOU! SUJOU!",
            "Ta chovendo viatura, acelera!",
            "Eu falei pra nao fazer isso aqui!",
            "Ah pronto, agora sim.",
        };

        public static readonly string[] Escaped =
        {
            "HAHA! Despistamos!",
            "Somos bons demais nisso.",
            "Proxima vez eu dirijo.",
        };

        public static readonly string[] PartnerKill =
        {
            "Esse ai ja era.",
            "Um a menos.",
            "Viu isso? Viu?!",
        };

        public static readonly string[] HurtByPlayer =
        {
            "EI! Ta maluco?!",
            "Mira pro outro lado, ze!",
            "Faz isso de novo pra voce ver.",
        };

        public static readonly string[] Abandoned =
        {
            "Vai me deixar aqui mesmo?!",
            "Volta aqui!",
        };

        public static readonly string[] Warning =
        {
            "Ta ficando dificil confiar em voce...",
            "Mais uma dessas e eu to fora.",
        };

        public static readonly string[] BetrayalWindup =
        {
            "Sabe de uma coisa? Cansei de voce.",
            "Nada pessoal... sao negocios.",
            "Desculpa, parceiro. A recompensa ta alta.",
        };

        public static string Pick(Random rng, string[] list) => list[rng.Next(list.Length)];
    }
}
