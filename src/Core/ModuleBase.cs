namespace TropicalMod.Core
{
    /// <summary>
    /// Cada mecânica do mod é um módulo. Para adicionar uma nova:
    /// crie uma classe em src/Modules herdando de ModuleBase e registre em Main.cs.
    /// </summary>
    public abstract class ModuleBase
    {
        public abstract string Name { get; }
        public virtual string Description => "";
        public virtual bool EnabledByDefault => true;

        public bool Enabled { get; private set; }
        internal bool Started { get; set; }

        protected ModuleBase()
        {
            Enabled = EnabledByDefault;
        }

        public void SetEnabled(bool value)
        {
            if (value == Enabled) return;
            Enabled = value;
            if (value) OnEnable(); else OnDisable();
        }

        /// <summary>Chamado uma vez, no primeiro tick com o jogo carregado.</summary>
        public virtual void OnStart() { }

        /// <summary>Chamado todo frame enquanto o módulo está ligado.</summary>
        public virtual void OnTick() { }

        /// <summary>Tecla pressionada (só chamado com o módulo ligado e o menu fechado).</summary>
        public virtual void OnKeyDown(System.Windows.Forms.KeyEventArgs e) { }

        public virtual void OnEnable() { }
        public virtual void OnDisable() { }

        /// <summary>Chamado ao recarregar scripts ou sair do jogo: limpe peds, blips, props aqui.</summary>
        public virtual void OnStop() { }
    }
}
