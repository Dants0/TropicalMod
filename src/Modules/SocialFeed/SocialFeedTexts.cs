using System;
using System.Collections.Generic;

namespace TropicalMod.Modules.SocialFeed
{
    public enum CrimeKind
    {
        Kill,
        CopKill,
        CarJack,
        WantedUp,
        Escaped,
        Speeding,
        Stunt,
        Wasted,
        Busted,
    }

    /// <summary>
    /// Textos dos posts. Para o devlog: é aqui que entra o humor — dá pra pedir sugestões nos comentários
    /// e adicionar as melhores. {stars} é trocado pelo nível de procurado.
    /// </summary>
    public static class SocialFeedTexts
    {
        public static readonly string[] Handles =
        {
            "@tia_do_zap", "@fofoqueira_de_rockford", "@motoboy_ls", "@vespucci_vibes",
            "@paulinho.camera", "@crente_de_sandy", "@grove_st_noticias", "@vinewood.gata",
            "@coach_quantico", "@seu_ze_da_banca", "@gamer_de_paleto", "@ls_alerta_24h",
            "@influencer_falido", "@tiozao_do_churras", "@turista_perdido", "@bombeiro_de_folga",
        };

        private static readonly Dictionary<CrimeKind, string[]> Templates = new Dictionary<CrimeKind, string[]>
        {
            [CrimeKind.Kill] = new[]
            {
                "acabei de ver um cara apagar outro no meio da rua. LS ta impossivel",
                "gente o maluco simplesmente... eu to tremendo",
                "alguem conhece esse doido? foi aqui perto de casa",
                "minha mae disse pra eu nao sair hoje. ela tava certa",
            },
            [CrimeKind.CopKill] = new[]
            {
                "ELE DERRUBOU UM POLICIAL AO VIVO",
                "a LSPD vai ficar MUITO brava com isso",
                "eu so queria comprar pao e vi ISSO",
            },
            [CrimeKind.CarJack] = new[]
            {
                "o cara tirou a pessoa do carro e saiu cantando pneu kkkkk",
                "esse carro... era MEU carro...",
                "roubo de carro em plena luz do dia, cade a policia?",
                "test drive agressivo demais",
            },
            [CrimeKind.WantedUp] = new[]
            {
                "helicoptero passando aqui na rua, o que ta acontecendo??",
                "{stars} estrelas e subindo, alguem para esse homem",
                "tem viatura pra todo lado, fiquem em casa",
            },
            [CrimeKind.Escaped] = new[]
            {
                "a policia perdeu ele DE NOVO. lenda",
                "fugiu de {stars} estrelas e ninguem vai falar nada?",
                "a LSPD precisa de um curso urgente",
            },
            [CrimeKind.Speeding] = new[]
            {
                "passou um carro aqui a uns 200 por hora, quase morri",
                "radar? nao conheco",
                "o motoboy mais rapido de LS",
            },
            [CrimeKind.Stunt] = new[]
            {
                "O CARRO VOOU. eu filmei. vai viralizar",
                "isso nao e fisica, e fe",
                "aterrissou melhor que aviao da LS Air",
            },
            [CrimeKind.Wasted] = new[]
            {
                "e assim termina a carreira do influencer mais caotico de LS",
                "F no chat",
                "ele tinha tanto potencial...",
            },
            [CrimeKind.Busted] = new[]
            {
                "PRESO AO VIVO kkkkkkk",
                "vai fazer live da cadeia agora",
                "a casa caiu pro nosso amigo",
            },
        };

        /// <summary>Seguidores base que cada tipo de crime rende (antes de multiplicadores).</summary>
        public static int BaseFollowers(CrimeKind kind)
        {
            switch (kind)
            {
                case CrimeKind.Kill: return 40;
                case CrimeKind.CopKill: return 150;
                case CrimeKind.CarJack: return 60;
                case CrimeKind.WantedUp: return 100;
                case CrimeKind.Escaped: return 400;
                case CrimeKind.Speeding: return 50;
                case CrimeKind.Stunt: return 120;
                default: return 0;
            }
        }

        public static string RandomText(Random rng, CrimeKind kind, int stars)
        {
            var list = Templates[kind];
            return list[rng.Next(list.Length)].Replace("{stars}", stars.ToString());
        }

        public static string RandomHandle(Random rng) => Handles[rng.Next(Handles.Length)];

        /// <summary>1234 → "1,2 mil"; 3400000 → "3,4 mi".</summary>
        public static string FormatCount(long n)
        {
            if (n >= 1_000_000) return (n / 1_000_000.0).ToString("0.#") + " mi";
            if (n >= 1_000) return (n / 1_000.0).ToString("0.#") + " mil";
            return n.ToString();
        }
    }
}
