namespace Sitora.Domain.Common;

// Claims access-токена, по которым ограничивается доступ к данным.
public static class AccessClaims
{
    public const string Role = "role";
    public const string Organization = "org";

    // По одному claim на доступный дом; если их нет — доступны все дома организации.
    public const string Building = "bld";

    // Id создателя платформы, если токен выдан в режиме «войти как владелец».
    public const string ImpersonatedBy = "imp";
}
