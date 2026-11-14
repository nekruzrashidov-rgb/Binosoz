namespace Sitora.Domain.Enums;

// Класс энергоэффективности дома (от лучшего к худшему). В JSON — имя значения: APlusPlus, APlus, A, B, C, D.
public enum EnergyClass
{
    APlusPlus,
    APlus,
    A,
    B,
    C,
    D
}

public enum HeatingType
{
    Central,
    IndividualGas,
    HeatPump,
    Electric
}
