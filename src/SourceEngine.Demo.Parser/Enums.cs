namespace SourceEngine.Demo.Parser
{
    public enum RoundEndReason
    {
        /// <summary>
        /// Target Successfully Bombed!
        /// </summary>
        TargetBombed = 1,

        /// <summary>
        /// The VIP has escaped.
        /// </summary>
        VIPEscaped,

        /// <summary>
        /// VIP has been assassinated
        /// </summary>
        VIPKilled,

        /// <summary>
        /// The terrorists have escaped
        /// </summary>
        TerroristsEscaped,

        /// <summary>
        /// The CTs have prevented most of the terrorists from escaping!
        /// </summary>
        CTStoppedEscape,

        /// <summary>
        /// Escaping terrorists have all been neutralized
        /// </summary>
        TerroristsStopped,

        /// <summary>
        /// The bomb has been defused!
        /// </summary>
        BombDefused,

        /// <summary>
        /// Counter-Terrorists Win!
        /// </summary>
        CTWin,

        /// <summary>
        /// Terrorists Win!
        /// </summary>
        TerroristWin,

        /// <summary>
        /// Round Draw!
        /// </summary>
        Draw,

        /// <summary>
        /// All Hostages have been rescued
        /// </summary>
        HostagesRescued,

        /// <summary>
        /// Target has been saved!
        /// </summary>
        TargetSaved,

        /// <summary>
        /// Hostages have not been rescued!
        /// </summary>
        HostagesNotRescued,

        /// <summary>
        /// Terrorists have not escaped!
        /// </summary>
        TerroristsNotEscaped,

        /// <summary>
        /// VIP has not escaped!
        /// </summary>
        VIPNotEscaped,

        /// <summary>
        /// Game Commencing!
        /// </summary>
        GameStart,

        /// <summary>
        /// Terrorists Surrender
        /// </summary>
        TerroristsSurrender,

        /// <summary>
        /// CTs Surrender
        /// </summary>
        CTSurrender,

        /// <summary>
        /// SurvivalWin
        /// </summary>
        SurvivalWin = 21, // danger zone

        /// <summary>
        /// Unknown
        /// </summary>
        Unknown, // Caused by an error where the round_end event was not triggered for a round
    }

    public enum RoundMVPReason
    {
        MostEliminations = 1,
        BombPlanted,
        BombDefused,
    }

    public enum Hitgroup
    {
        Generic = 0,
        Head = 1,
        Chest = 2,
        Stomach = 3,
        LeftArm = 4,
        RightArm = 5,
        LeftLeg = 6,
        RightLeg = 7,
        Gear = 10,
    }
}
