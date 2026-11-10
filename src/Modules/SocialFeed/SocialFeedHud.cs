using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.UI;
using Font = GTA.UI.Font;

namespace TropicalMod.Modules.SocialFeed
{
    public class FeedPost
    {
        public string Handle;
        public string Text;
        public long LikesTarget;
        public long Likes;
        public int CreatedAt;
        public bool Negative;
    }

    /// <summary>
    /// Desenha o "celular" com o feed no canto direito da tela.
    /// Coordenadas no espaço base do SHVDN (1280x720), escala automática para qualquer resolução.
    /// </summary>
    public class SocialFeedHud
    {
        public const string AppName = "Bafafá";

        private const float Width = 300f;
        private const float X = 1280f - Width - 20f;
        private const float Y = 110f;
        private const float HeaderH = 46f;
        private const float PostH = 62f;
        private const float Gap = 4f;

        private static readonly Color Bg = Color.FromArgb(215, 18, 18, 24);
        private static readonly Color HeaderBg = Color.FromArgb(230, 120, 40, 200);
        private static readonly Color PostBg = Color.FromArgb(200, 32, 32, 42);
        private static readonly Color PostNew = Color.FromArgb(220, 60, 45, 90);

        public void Draw(IReadOnlyList<FeedPost> posts, long followers, int combo, int now)
        {
            var header = new ContainerElement(new PointF(X, Y), new SizeF(Width, HeaderH), HeaderBg);
            header.Items.Add(new TextElement(AppName, new PointF(10f, 4f), 0.5f, Color.White, Font.ChaletComprimeCologne));
            header.Items.Add(new TextElement(
                SocialFeedTexts.FormatCount(followers) + " seguidores",
                new PointF(Width - 10f, 10f), 0.38f, Color.White, Font.ChaletLondon, Alignment.Right));

            if (combo > 1)
            {
                header.Items.Add(new TextElement(
                    "~y~VIRALIZANDO x" + combo,
                    new PointF(10f, 28f), 0.28f, Color.White, Font.ChaletLondon));
            }
            header.Draw();

            float y = Y + HeaderH + Gap;
            foreach (var post in posts)
            {
                bool fresh = now - post.CreatedAt < 1500;
                var box = new ContainerElement(new PointF(X, y), new SizeF(Width, PostH), fresh ? PostNew : PostBg);

                box.Items.Add(new TextElement(post.Handle, new PointF(10f, 4f), 0.3f,
                    Color.FromArgb(255, 190, 150, 255), Font.ChaletLondon));

                box.Items.Add(new TextElement(post.Text, new PointF(10f, 20f), 0.28f, Color.White, Font.ChaletLondon)
                {
                    WrapWidth = Width - 20f,
                });

                string likes = post.Negative
                    ? "~r~" + SocialFeedTexts.FormatCount(post.Likes) + " risadas"
                    : "~p~" + SocialFeedTexts.FormatCount(post.Likes) + " curtidas";
                box.Items.Add(new TextElement(likes, new PointF(Width - 10f, 4f), 0.28f, Color.White,
                    Font.ChaletLondon, Alignment.Right));

                box.Draw();
                y += PostH + Gap;
            }
        }
    }
}
