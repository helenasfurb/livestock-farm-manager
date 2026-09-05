using System.ComponentModel;

namespace MuuBoi.Domain.Enums
{
    /// <summary>
    /// Dose type of a vaccination event. Stored on the event; the default comes from the
    /// lineage (an event without a parent is born FirstDose; a booster spawn is born Booster)
    /// and is editable.
    /// </summary>
    public enum DoseType
    {
        [Description("Primeira dose")]
        FirstDose = 1,

        [Description("Reforço")]
        Booster = 2
    }
}
