using Content.Shared.Body.Part;

namespace Content.Client.Body.Part;

/// <summary>
/// Client-side implementation of SharedBodyPartSystem. Exists so that BodySystem and other
/// shared code can resolve SharedBodyPartSystem on the client; all behavior is in the base class.
/// </summary>
public sealed class ClientBodyPartSystem : SharedBodyPartSystem
{
}
