namespace SourceEngine.Demo.Parser
{
    /// <summary>
    /// Reasons for which a round can end.
    /// </summary>
    public enum RoundEndReason
    {
        Invalid = -1,

        /// <summary>
        /// The round hasn't ended yet.
        /// </summary>
        StillInProgress,

        /// <summary>
        /// Target successfully bombed!
        /// </summary>
        TargetBombed,

        /// <summary>
        /// The VIP has escaped.
        /// </summary>
        VIPEscaped,

        /// <summary>
        /// VIP has been assassinated.
        /// </summary>
        VIPAssassinated,

        /// <summary>
        /// The terrorists have escaped!
        /// </summary>
        TerroristsEscaped,

        /// <summary>
        /// The CTs have prevented most of the terrorists from escaping.
        /// </summary>
        CTsPreventedEscape,

        /// <summary>
        /// Escaping terrorists have all been neutralized.
        /// </summary>
        EscapingTerroristsNeutralized,

        /// <summary>
        /// The bomb has been defused!
        /// </summary>
        BombDefused,

        /// <summary>
        /// Counter-Terrorists win!
        /// </summary>
        CTsWin,

        /// <summary>
        /// Terrorists win!
        /// </summary>
        TerroristsWin,

        /// <summary>
        /// Round draw!
        /// </summary>
        RoundDraw,

        /// <summary>
        /// All hostages have been rescued.
        /// </summary>
        HostagesRescued,

        /// <summary>
        /// Bombing failed.
        /// </summary>
        TargetSaved,

        /// <summary>
        /// Hostages have not been rescued!
        /// </summary>
        HostagesNotRescued,

        /// <summary>
        /// Terrorists have not escaped.
        /// </summary>
        TerroristsNotEscaped,

        /// <summary>
        /// VIP has not escaped.
        /// </summary>
        VIPNotEscaped,

        /// <summary>
        /// Game commencing!
        /// </summary>
        GameCommencing,

        /// <summary>
        /// Terrorists surrender.
        /// </summary>
        TerroristsSurrender,

        /// <summary>
        /// CTs surrender.
        /// </summary>
        CTsSurrender,

        /// <summary>
        /// Terrorists planted the bomb.
        /// </summary>
        TerroristsPlanted,

        /// <summary>
        /// CTs have reached a hostage.
        /// </summary>
        CTsReachedHostage,

        /// <summary>
        /// Danger Zone win.
        /// </summary>
        SurvivalWin,

        /// <summary>
        /// Unknown
        /// </summary>
        /// <remarks>
        /// Caused by an error where the round_end event was not triggered for a round.
        /// </remarks>
        Unknown,
    }

    /// <summary>
    /// Reasons for which a player can be awarded MVP for a round.
    /// </summary>
    public enum RoundMVPReason
    {
        Undefined,
        MostEliminations,
        BombPlanted,
        BombDefused,
        HostageRescued,
        GunGameWinner,
    }

    /// <summary>
    /// Regions of the body which can take damage.
    /// </summary>
    public enum HitGroup
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
