using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Animals;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Floofstation.Leash.Components;
using Content.Shared.FloofStation.Traits;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.FloofStation.Traits;

public sealed class MilkerSystem : EntitySystem
{
    private static readonly SpriteSpecifier.Rsi DefaultLinkSprite = new(new ResPath("/Textures/Floof/Objects/Tools/leash-rope.rsi"), "rope");
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string MilkerContainerId = "milker";
    private const string AttachedBreastState = "attached-breast";
    private const string AttachedGroinState = "attached-groin";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MilkerComponent, ComponentStartup>(OnMilkerStartup);
        SubscribeLocalEvent<MilkerComponent, ComponentShutdown>(OnMilkerShutdown);
        SubscribeLocalEvent<MilkerComponent, EntInsertedIntoContainerMessage>(OnMilkerInserted);
        SubscribeLocalEvent<MilkProducerComponent, GetVerbsEvent<InteractionVerb>>(OnMilkVerbs);
        SubscribeLocalEvent<UdderComponent, GetVerbsEvent<InteractionVerb>>(OnUdderVerbs);
        SubscribeLocalEvent<CumProducerComponent, GetVerbsEvent<InteractionVerb>>(OnCumVerbs);
    }

    private void OnMilkerStartup(Entity<MilkerComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.EnsureOwnSolution)
            _solution.EnsureSolution(ent.Owner, ent.Comp.SolutionName, out _);
    }

    private void OnMilkerInserted(Entity<MilkerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.LinkToContainerSlotId == null || args.Container.ID != ent.Comp.LinkToContainerSlotId)
            return;

        ent.Comp.LinkedEntity = args.Container.Owner;
        Dirty(ent);
    }

    private void OnMilkerShutdown(Entity<MilkerComponent> ent, ref ComponentShutdown args)
    {
        SetMilkerActiveVisual(ent.Owner, ent.Comp, false);
        RemoveLinkVisual(ent);

        if (ent.Comp.AttachedTo is not { } target)
            return;

        if (TerminatingOrDeleted(target))
            return;

        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        _container.Remove(ent.Owner, targetContainer, force: true);
        if (ShouldUseAttachedVisuals(target, ent.Comp.Mode))
            RemoveAttachedVisual(target, ent.Comp);
    }

    private void OnMilkVerbs(Entity<MilkProducerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;

        if (args.Using is { } used &&
            TryComp<MilkerComponent>(used, out var milker) &&
            milker.AttachedTo == null &&
            !HasAttachedMilkerForMode(ent, MilkerMode.Milk))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("milker-verb-attach-breasts"),
                Act = () => TryAttach(user, used, ent, MilkerMode.Milk),
            });
        }

        AddDetachVerb(ent, args);
    }

    private void OnCumVerbs(Entity<CumProducerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;

        if (args.Using is { } used &&
            TryComp<MilkerComponent>(used, out var milker) &&
            milker.AttachedTo == null &&
            !HasAttachedMilkerForMode(ent, MilkerMode.Cum))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("milker-verb-attach-cock"),
                Act = () => TryAttach(user, used, ent, MilkerMode.Cum),
            });
        }

        AddDetachVerb(ent, args);
    }

    private void OnUdderVerbs(Entity<UdderComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;

        if (args.Using is { } used &&
            TryComp<MilkerComponent>(used, out var milker) &&
            milker.AttachedTo == null &&
            !HasAttachedMilkerForMode(ent, MilkerMode.Milk))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("milker-verb-attach-udder"),
                Act = () => TryAttach(user, used, ent, MilkerMode.Milk),
            });
        }

        AddDetachVerb(ent, args);
    }

    private bool HasAttachedMilkerForMode(EntityUid target, MilkerMode mode)
    {
        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        foreach (var attached in targetContainer.ContainedEntities)
        {
            if (!TryComp<MilkerComponent>(attached, out var milker) || milker.AttachedTo != target)
                continue;

            if (milker.Mode == mode)
                return true;
        }

        return false;
    }

    private void AddDetachVerb(EntityUid target, GetVerbsEvent<InteractionVerb> args)
    {
        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        foreach (var attached in targetContainer.ContainedEntities)
        {
            if (!TryComp<MilkerComponent>(attached, out var milker) || milker.AttachedTo != target)
                continue;

            var attachedMilker = attached;
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(milker.Mode == MilkerMode.Milk ? "milker-verb-remove-breasts" : "milker-verb-remove-cock"),
                Act = () => Detach(args.User, attachedMilker, target),
            });
        }
    }

    private void TryAttach(EntityUid user, EntityUid milkerUid, EntityUid target, MilkerMode mode)
    {
        if (!TryComp<MilkerComponent>(milkerUid, out var milker) || milker.AttachedTo != null)
            return;

        if (HasAttachedMilkerForMode(target, mode))
            return;

        if (TryComp<MilkerAttachedComponent>(target, out var attached))
        {
            if (mode.Equals(MilkerMode.Milk) && attached.BreastAttached || mode.Equals(MilkerMode.Cum) && attached.GroinAttached)
            {
                _popup.PopupEntity(Loc.GetString("milker-popup-already-attached"), target, user, PopupType.Medium);
                return;
            }
        }

        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        if (!_container.Insert(milkerUid, targetContainer))
            return;

        milker.AttachedTo = target;
        milker.Mode = mode;
        milker.NextTransfer = _timing.CurTime;
        Dirty(milkerUid, milker);
        if (ShouldUseAttachedVisuals(target, mode))
            AddAttachedVisual(target, milker);
        EnsureLinkVisual(milkerUid, milker, target);

        _popup.PopupEntity(Loc.GetString("milker-popup-attached"), target, user, PopupType.Medium);
    }

    private void Detach(EntityUid user, EntityUid milkerUid, EntityUid target)
    {
        if (!TryComp<MilkerComponent>(milkerUid, out var milker) || milker.AttachedTo != target)
            return;

        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        if (!_container.Remove(milkerUid, targetContainer))
            return;

        milker.AttachedTo = null;
        SetMilkerActiveVisual(milkerUid, milker, false);
        Dirty(milkerUid, milker);
        RemoveLinkVisual((milkerUid, milker));
        if (ShouldUseAttachedVisuals(target, milker.Mode))
            RemoveAttachedVisual(target, milker);
        _hands.PickupOrDrop(user, milkerUid);
        _popup.PopupEntity(Loc.GetString("milker-popup-detached"), target, user, PopupType.Medium);
    }

    private bool ShouldUseAttachedVisuals(EntityUid target, MilkerMode mode)
    {
        if (mode == MilkerMode.Cum)
            return true;

        return HasComp<MilkProducerComponent>(target);
    }

    private void AddAttachedVisual(EntityUid target, MilkerComponent milker)
    {
        var attached = EnsureComp<MilkerAttachedComponent>(target);
        if (milker.Mode == MilkerMode.Milk)
            attached.BreastAttached = true;
        else
            attached.GroinAttached = true;

        EnsureAttachedVisualEntity(target, attached, milker.MilkerAttachedPrototype);
        SetAttachedVisualState(attached);
        Dirty(target, attached);
    }

    private void RemoveAttachedVisual(EntityUid target, MilkerComponent milker)
    {
        if (!TryComp<MilkerAttachedComponent>(target, out var attached))
            return;

        if (milker.Mode == MilkerMode.Milk)
            attached.BreastAttached = false;
        else
            attached.GroinAttached = false;

        if (!attached.BreastAttached && !attached.GroinAttached)
        {
            if (attached.VisualEntity is { } visual && !TerminatingOrDeleted(visual))
                QueueDel(visual);

            RemCompDeferred<MilkerAttachedComponent>(target);
            return;
        }

        EnsureAttachedVisualEntity(target, attached, milker.MilkerAttachedPrototype);
        SetAttachedVisualState(attached);
        Dirty(target, attached);
    }

    private void EnsureAttachedVisualEntity(EntityUid target, MilkerAttachedComponent attached, string attachedVisualPrototype)
    {
        if (attached.VisualEntity is { } visual && !TerminatingOrDeleted(visual))
            return;

        var attachedVisual = Spawn(attachedVisualPrototype, Transform(target).Coordinates);
        _transform.SetParent(attachedVisual, target);
        attached.VisualEntity = attachedVisual;
    }

    private void SetAttachedVisualState(MilkerAttachedComponent attached)
    {
        if (attached.VisualEntity is not { } visual || TerminatingOrDeleted(visual))
            return;

        _appearance.SetData(visual, MilkerVisuals.BreastAttached, attached.BreastAttached);
        _appearance.SetData(visual, MilkerVisuals.GroinAttached, attached.GroinAttached);
    }

    private void EnsureLinkVisual(EntityUid milkerUid, MilkerComponent milker, EntityUid attachedTarget)
    {
        if (milker.LinkedEntity is not { } linkedEntity || TerminatingOrDeleted(linkedEntity))
        {
            RemoveLinkVisual((milkerUid, milker));
            return;
        }

        if (milker.LinkVisualEntity is not { } linkVisual || TerminatingOrDeleted(linkVisual))
        {
            linkVisual = Spawn(null, Transform(attachedTarget).Coordinates);
            milker.LinkVisualEntity = linkVisual;
            Dirty(milkerUid, milker);
        }

        var visuals = EnsureComp<LeashedVisualsComponent>(linkVisual);
        visuals.Source = attachedTarget;
        visuals.Target = linkedEntity;
        visuals.Sprite = milker.LinkSprite ?? DefaultLinkSprite;
        Dirty(linkVisual, visuals);
    }

    private void RemoveLinkVisual(Entity<MilkerComponent> milker)
    {
        if (milker.Comp.LinkVisualEntity is { } linkVisual && !TerminatingOrDeleted(linkVisual))
            QueueDel(linkVisual);

        milker.Comp.LinkVisualEntity = null;
        Dirty(milker);
    }

    private void SetMilkerActiveVisual(EntityUid milkerUid, MilkerComponent milker, bool active)
    {
        if (milker.IsActivelyDrawing == active)
            return;

        milker.IsActivelyDrawing = active;
        Dirty(milkerUid, milker);

        var visualTarget = milker.LinkedEntity is { } linkedEntity && !TerminatingOrDeleted(linkedEntity)
            ? linkedEntity
            : milkerUid;

        _appearance.SetData(visualTarget, MilkerVisuals.Active, active);
    }

    private bool ResolveOutputSolution(EntityUid milkerUid, ref MilkerComponent milker, out Entity<SolutionComponent> outputSolutionEntity, out Solution outputSolution)
    {
        if (milker.LinkedEntity is { } linkedEntity && !TerminatingOrDeleted(linkedEntity))
        {
            Entity<SolutionComponent>? linkedSolution = null;
            var linkedSolutionName = milker.LinkedSolutionName ?? milker.SolutionName;
            if (_solution.ResolveSolution(linkedEntity, linkedSolutionName, ref linkedSolution, out var linkedOutputSolution) && linkedSolution != null)
            {
                outputSolution = linkedOutputSolution;
                outputSolutionEntity = linkedSolution.Value;
                return true;
            }
        }

        if (_solution.ResolveSolution(milkerUid, milker.SolutionName, ref milker.Solution, out var localOutputSolution) && milker.Solution != null)
        {
            outputSolution = localOutputSolution;
            outputSolutionEntity = milker.Solution.Value;
            return true;
        }

        outputSolutionEntity = default;
        outputSolution = default!;
        return false;
    }

    private bool CanOperate(MilkerComponent milker)
    {
        if (milker.LinkedEntity is not { } linkedEntity || TerminatingOrDeleted(linkedEntity))
            return true;

        if (!Transform(linkedEntity).Anchored)
            return false;

        if (TryComp<ApcPowerReceiverComponent>(linkedEntity, out var receiver) && !_power.IsPowered(linkedEntity, receiver))
            return false;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<MilkerComponent>();
        var now = _timing.CurTime;

        while (query.MoveNext(out var uid, out var milker))
        {
            if (milker.AttachedTo is not { } target || now < milker.NextTransfer)
                continue;

            milker.NextTransfer = now + milker.TransferDelay;

            if (TerminatingOrDeleted(target))
            {
                RemoveLinkVisual((uid, milker));
                milker.AttachedTo = null;
                SetMilkerActiveVisual(uid, milker, false);
                Dirty(uid, milker);
                continue;
            }

            EnsureLinkVisual(uid, milker, target);

            if (!CanOperate(milker))
            {
                SetMilkerActiveVisual(uid, milker, false);
                continue;
            }

            if (!ResolveOutputSolution(uid, ref milker, out var targetSolutionEntity, out var targetSolution))
            {
                SetMilkerActiveVisual(uid, milker, false);
                continue;
            }

            Entity<SolutionComponent>? sourceSolutionEntity = null;
            FixedPoint2 sourceVolume = FixedPoint2.Zero;
            string reagentId;
            string source;
            if (milker.Mode == MilkerMode.Milk && TryComp<MilkProducerComponent>(target, out var milkProducer))
            {
                if (_solution.ResolveSolution(target, milkProducer.SolutionName, ref milkProducer.Solution, out var milkSolution))
                {
                    sourceSolutionEntity = milkProducer.Solution;
                    sourceVolume = milkSolution.Volume;
                }

                reagentId = milkProducer.ReagentId;
                source = "breasts";
            }
            else if (milker.Mode == MilkerMode.Milk && TryComp<UdderComponent>(target, out var udder))
            {
                if (_solution.ResolveSolution(target, udder.SolutionName, ref udder.Solution, out var udderSolution))
                {
                    sourceSolutionEntity = udder.Solution;
                    sourceVolume = udderSolution.Volume;
                }

                reagentId = udder.ReagentId;
                source = "udder";
            }
            else if (milker.Mode == MilkerMode.Cum && TryComp<CumProducerComponent>(target, out var cumProducer))
            {
                if (_solution.ResolveSolution(target, cumProducer.SolutionName, ref cumProducer.Solution, out var cumSolution))
                {
                    sourceSolutionEntity = cumProducer.Solution;
                    sourceVolume = cumSolution.Volume;
                }

                reagentId = cumProducer.ReagentId;
                source = "groin";
            }
            else
            {
                SetMilkerActiveVisual(uid, milker, false);
                continue;
            }

            if (sourceSolutionEntity == null)
            {
                SetMilkerActiveVisual(uid, milker, false);
                continue;
            }

            var amount = milker.QuantityPerUpdate;
            if (amount > sourceVolume)
                amount = sourceVolume;

            if (amount > targetSolution.AvailableVolume)
                amount = targetSolution.AvailableVolume;

            if (amount <= FixedPoint2.Zero)
            {
                SetMilkerActiveVisual(uid, milker, false);
                continue;
            }

            var transferPopup = Loc.GetString("milker-popup-transfer-tick", ("amount", amount), ("chemical", reagentId), ("source", source));

            _popup.PopupEntity(transferPopup, target, target);


            var split = _solution.SplitSolution(sourceSolutionEntity.Value, amount);
            _solution.TryAddSolution(targetSolutionEntity, split);
            SetMilkerActiveVisual(uid, milker, true);
        }
    }
}
