using System.Drawing;
using GTA;
using GTA.UI;
using TropicalMod.Core;

namespace TropicalMod.Modules
{
    /// <summary>
    /// Ferramenta de desenvolvimento: mostra posição, direção e veículo do jogador.
    /// Útil para anotar coordenadas de spawn durante o devlog.
    /// </summary>
    public class DebugHudModule : ModuleBase
    {
        public override string Name => "Debug HUD";
        public override string Description => "Mostra coordenadas e info do jogador na tela.";
        public override bool EnabledByDefault => false;

        private readonly TextElement _text = new TextElement("", new PointF(10f, 10f), 0.35f)
        {
            Outline = true,
        };

        public override void OnTick()
        {
            var ped = Game.Player.Character;
            var p = ped.Position;
            string veh = ped.IsInVehicle() ? ped.CurrentVehicle.DisplayName : "a pé";

            _text.Caption =
                $"X {p.X:F1}  Y {p.Y:F1}  Z {p.Z:F1}\n" +
                $"Heading {ped.Heading:F0}  |  {veh}  |  Wanted {Game.Player.WantedLevel}";
            _text.Draw();
        }
    }
}
