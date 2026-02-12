// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Princess Cheeseballs <66055347+Princess-Cheeseballs@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Hands.Components;
using Content.Shared.Stunnable;

namespace Content.Shared.Hands.EntitySystems;

/// <summary>
/// This is for events that don't affect normal hand functions but do care about hands.
/// </summary>
public abstract partial class SharedHandsSystem
{
    private void InitializeEventListeners()
    {
        SubscribeLocalEvent<HandsComponent, GetHandCountEvent>(OnGetHandCount);
        SubscribeLocalEvent<HandsComponent, GetStandUpTimeEvent>(OnStandupArgs);
        SubscribeLocalEvent<HandsComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh);
    }

    private void OnGetHandCount(Entity<HandsComponent> ent, ref GetHandCountEvent args)
    {
        args.TotalCount = ent.Comp.Count;
        args.EmptyCount = CountFreeHands(ent.AsNullable());
    }

    /// <summary>
    /// Reduces the time it takes to stand up based on the number of hands we have available.
    /// </summary>
    private void OnStandupArgs(Entity<HandsComponent> ent, ref GetStandUpTimeEvent time)
    {
        if (!HasComp<KnockedDownComponent>(ent))
            return;

        var ev = new GetHandCountEvent();
        RaiseLocalEvent(ent.Owner, ref ev);

        if (ev.TotalCount == 0 || ev.EmptyCount == 0)
            return;

        time.DoAfterTime *= (float)ev.TotalCount / (ev.EmptyCount + ev.TotalCount);
    }

    private void OnKnockedDownRefresh(Entity<HandsComponent> ent, ref KnockedDownRefreshEvent args)
    {
        var ev = new GetHandCountEvent();
        RaiseLocalEvent(ent.Owner, ref ev);

        // Can't crawl around without any hands.
        // Entities without the HandsComponent will always have full crawling speed.
        if (ev.TotalCount == 0)
            args.SpeedModifier = 0f;
        else
            args.SpeedModifier *= (float)ev.EmptyCount / ev.TotalCount;
    }
}
