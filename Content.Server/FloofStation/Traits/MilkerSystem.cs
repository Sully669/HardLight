using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.FloofStation.Traits;

public sealed class MilkerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string MilkerContainerId = "milker";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MilkerComponent, ComponentStartup>(OnMilkerStartup);
        SubscribeLocalEvent<MilkerComponent, ComponentShutdown>(OnMilkerShutdown);
        SubscribeLocalEvent<MilkProducerComponent, GetVerbsEvent<InteractionVerb>>(OnMilkVerbs);
        SubscribeLocalEvent<CumProducerComponent, GetVerbsEvent<InteractionVerb>>(OnCumVerbs);
    }

    private void OnMilkerStartup(Entity<MilkerComponent> ent, ref ComponentStartup args)
    {
        _solution.EnsureSolution(ent.Owner, ent.Comp.SolutionName, out _);
    }

    private void OnMilkerShutdown(Entity<MilkerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AttachedTo is not { } target)
            return;

        if (TerminatingOrDeleted(target))
            return;

        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        _container.Remove(ent.Owner, targetContainer, force: true);
    }

    private void OnMilkVerbs(Entity<MilkProducerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;

        if (args.Using is { } used && TryComp<MilkerComponent>(used, out var milker) && milker.AttachedTo == null)
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

        if (args.Using is { } used && TryComp<MilkerComponent>(used, out var milker) && milker.AttachedTo == null)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("milker-verb-attach-cock"),
                Act = () => TryAttach(user, used, ent, MilkerMode.Cum),
            });
        }

        AddDetachVerb(ent, args);
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
                Text = Loc.GetString("milker-verb-remove"),
                Act = () => Detach(args.User, attachedMilker, target),
            });
            break;
        }
    }

    private void TryAttach(EntityUid user, EntityUid milkerUid, EntityUid target, MilkerMode mode)
    {
        if (!TryComp<MilkerComponent>(milkerUid, out var milker) || milker.AttachedTo != null)
            return;

        var targetContainer = _container.EnsureContainer<Container>(target, MilkerContainerId);
        if (!_container.Insert(milkerUid, targetContainer))
            return;

        milker.AttachedTo = target;
        milker.Mode = mode;
        milker.NextTransfer = _timing.CurTime;
        Dirty(milkerUid, milker);

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
        Dirty(milkerUid, milker);
        _hands.PickupOrDrop(user, milkerUid);
        _popup.PopupEntity(Loc.GetString("milker-popup-detached"), target, user, PopupType.Medium);
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
                milker.AttachedTo = null;
                Dirty(uid, milker);
                continue;
            }

            if (!_solution.ResolveSolution(uid, milker.SolutionName, ref milker.Solution, out var targetSolution))
                continue;

            Entity<SolutionComponent>? sourceSolutionEntity = null;
            FixedPoint2 sourceVolume = FixedPoint2.Zero;
            if (milker.Mode == MilkerMode.Milk && TryComp<MilkProducerComponent>(target, out var milkProducer))
            {
                if (_solution.ResolveSolution(target, milkProducer.SolutionName, ref milkProducer.Solution, out var milkSolution))
                {
                    sourceSolutionEntity = milkProducer.Solution;
                    sourceVolume = milkSolution.Volume;
                }
            }
            else if (milker.Mode == MilkerMode.Cum && TryComp<CumProducerComponent>(target, out var cumProducer))
            {
                if (_solution.ResolveSolution(target, cumProducer.SolutionName, ref cumProducer.Solution, out var cumSolution))
                {
                    sourceSolutionEntity = cumProducer.Solution;
                    sourceVolume = cumSolution.Volume;
                }
            }

            if (sourceSolutionEntity == null)
                continue;

            var amount = milker.QuantityPerUpdate;
            if (amount > sourceVolume)
                amount = sourceVolume;

            if (amount > targetSolution.AvailableVolume)
                amount = targetSolution.AvailableVolume;

            if (amount <= FixedPoint2.Zero)
                continue;

            var split = _solution.SplitSolution(sourceSolutionEntity.Value, amount);
            _solution.TryAddSolution(milker.Solution!.Value, split);
        }
    }
}
