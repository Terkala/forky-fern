using Content.Server.Chat.Systems;
using Content.Shared._CE.Actions;
using Content.Shared._CE.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chat;

namespace Content.Server._CE.Actions;

public sealed partial class CEActionSystem
{
    private void InitializeDoAfter()
    {
        SubscribeLocalEvent<CEActionSpeakingComponent, CEActionStartDoAfterEvent>(OnVerbalActionStarted);
        SubscribeLocalEvent<CEActionSpeakingComponent, ActionDoAfterEvent>(OnVerbalActionPerformed);

        SubscribeLocalEvent<CEActionEmotingComponent, CEActionStartDoAfterEvent>(OnEmoteActionStarted);
        SubscribeLocalEvent<CEActionEmotingComponent, ActionDoAfterEvent>(OnEmoteActionPerformed);
    }

    private void OnVerbalActionStarted(Entity<CEActionSpeakingComponent> ent, ref CEActionStartDoAfterEvent args)
    {
        var performer = GetEntity(args.Performer);
        _chat.TrySendInGameICMessage(performer, ent.Comp.StartSpeech, ent.Comp.Whisper ? InGameICChatType.Whisper : InGameICChatType.Speak, true);
    }

    private void OnEmoteActionStarted(Entity<CEActionEmotingComponent> ent, ref CEActionStartDoAfterEvent args)
    {
        var performer = GetEntity(args.Performer);
        _chat.TrySendInGameICMessage(performer, Loc.GetString(ent.Comp.StartEmote), InGameICChatType.Emote, true);
    }

    private void OnVerbalActionPerformed(Entity<CEActionSpeakingComponent> ent, ref ActionDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!args.Handled)
            return;

        var performer = GetEntity(args.Performer);
        _chat.TrySendInGameICMessage(performer, ent.Comp.EndSpeech, ent.Comp.Whisper ? InGameICChatType.Whisper : InGameICChatType.Speak, true);
    }

    private void OnEmoteActionPerformed(Entity<CEActionEmotingComponent> ent, ref ActionDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!args.Handled)
            return;

        var performer = GetEntity(args.Performer);
        _chat.TrySendInGameICMessage(performer, Loc.GetString(ent.Comp.EndEmote), InGameICChatType.Emote, true);
    }
}
