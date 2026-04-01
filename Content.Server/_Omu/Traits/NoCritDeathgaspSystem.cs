using Content.Server.Mobs;
using Content.Shared._Omu.Traits;
using Content.Shared.Mobs;

namespace Content.Server._Omu.Traits;

// Allows for the NoCrit system to do the Deathgasp on instant state change to death
public sealed class NoCritDeathgaspSystem : EntitySystem
{
    [Dependency] private readonly DeathgaspSystem _deathgasp = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NoCritComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, NoCritComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            _deathgasp.Deathgasp(uid);
        }
    }
}
