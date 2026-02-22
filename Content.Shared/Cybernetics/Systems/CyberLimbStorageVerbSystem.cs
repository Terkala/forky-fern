using Content.Shared.Body;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Adds "Open [limb name]" verbs when right-clicking a body with cyber limbs, when the maintenance panel is open and bolts are tight.
/// </summary>
public sealed class CyberLimbStorageVerbSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberneticsMaintenanceComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<CyberneticsMaintenanceComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var comp = ent.Comp;
        if (!comp.PanelOpen || !comp.BoltsTight)
            return;

        var body = ent.Owner;

        var user = args.User;
        foreach (var organ in _body.GetAllOrgans(body))
        {
            if (!HasComp<CyberLimbComponent>(organ) || !TryComp<StorageComponent>(organ, out var storage))
                continue;

            var limbUid = organ;
            var storageComp = storage;
            var limbName = Name(organ);
            var verb = new ActivationVerb
            {
                Act = () => _storage.OpenStorageUI(limbUid, user, storageComp, false),
                Text = Loc.GetString("cyber-maintenance-verb-open-limb", ("limbName", limbName)),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"))
            };
            args.Verbs.Add(verb);
        }
    }
}
