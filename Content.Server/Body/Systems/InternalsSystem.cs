using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Shared.Atmos;

namespace Content.Server.Body.Systems;

public sealed class InternalsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InternalsComponent, InhaleLocationEvent>(OnInhaleLocation);
    }

    private void OnInhaleLocation(EntityUid uid, InternalsComponent component, InhaleLocationEvent args)
    {
        if (component.AreInternalsWorking())
        {
            var gasTank = Comp<GasTankComponent>(component.GasTankEntity!.Value);
            args.Gas = gasTank.RemoveAirVolume(Atmospherics.BreathVolume);
        }
    }
    public void DisconnectBreathTool(InternalsComponent component)
    {
        var old = component.BreathToolEntity;
        component.BreathToolEntity = null;

        if (TryComp(old, out BreathToolComponent? breathTool) )
        {
            _atmos.DisconnectInternals(breathTool);
            DisconnectTank(component);
        }

        _alerts.ShowAlert(component.Owner, AlertType.Internals, GetSeverity(component));
    }

    public void ConnectBreathTool(InternalsComponent component, EntityUid toolEntity)
    {
        if (TryComp(component.BreathToolEntity, out BreathToolComponent? tool))
        {
            _atmos.DisconnectInternals(tool);
        }

        component.BreathToolEntity = toolEntity;
        _alerts.ShowAlert(component.Owner, AlertType.Internals, GetSeverity(component));
    }

    public void DisconnectTank(InternalsComponent? component)
    {
        if (component == null) return;

        if (TryComp(component.GasTankEntity, out GasTankComponent? tank))
        {
            _gasTank.DisconnectFromInternals(tank);
        }

        component.GasTankEntity = null;
        _alerts.ShowAlert(component.Owner, AlertType.Internals, GetSeverity(component));
    }

    public bool TryConnectTank(InternalsComponent component, EntityUid tankEntity)
    {
        if (component.BreathToolEntity == null)
            return false;

        if (TryComp(component.GasTankEntity, out GasTankComponent? tank))
        {
            _gasTank.DisconnectFromInternals(tank);
        }

        component.GasTankEntity = tankEntity;
        _alerts.ShowAlert(component.Owner, AlertType.Internals, GetSeverity(component));
        return true;
    }

    public bool AreInternalsWorking(InternalsComponent component)
    {
        return TryComp(component.BreathToolEntity, out BreathToolComponent? breathTool) &&
               breathTool.IsFunctional &&
               TryComp(component.GasTankEntity, out GasTankComponent? gasTank) &&
               gasTank.Air != null;
    }

    private short GetSeverity(InternalsComponent component)
    {
        if (component.BreathToolEntity == null || !AreInternalsWorking(component)) return 2;

        if (TryComp<GasTankComponent>(component.GasTankEntity, out var gasTank) && gasTank.Air.Volume < Atmospherics.BreathVolume)
            return 0;

        return 1;
    }

    public GasTankComponent? FindBestGasTank(InternalsComponent component)
    {
        // Prioritise
        // 1. exo-slot tanks
        // 2. in-hand tanks
        // 3. pocket tanks
        InventoryComponent? inventory = null;
        ContainerManagerComponent? containerManager = null;

        if (_inventory.TryGetSlotEntity(component.Owner, "suitstorage", out var entity, inventory, containerManager) &&
            TryComp<GasTankComponent>(entity, out var gasTank) &&
            _gasTank.CanConnectToInternals(gasTank))
        {
            return gasTank;
        }

        var tanks = new List<GasTankComponent>();

        foreach (var hand in _hands.EnumerateHands(component.Owner))
        {
            if (TryComp(hand.HeldEntity, out gasTank) && _gasTank.CanConnectToInternals(gasTank))
            {
                tanks.Add(gasTank);
            }
        }

        if (tanks.Count > 0)
        {
            tanks.Sort((x, y) => y.Air.TotalMoles.CompareTo(x.Air.TotalMoles));
            return tanks[0];
        }

        if (Resolve(component.Owner, ref inventory, false))
        {
            var enumerator = new InventorySystem.ContainerSlotEnumerator(component.Owner, inventory.TemplateId, _protoManager, _inventory, SlotFlags.POCKET);

            while (enumerator.MoveNext(out var container))
            {
                if (TryComp(container.ContainedEntity, out gasTank) && _gasTank.CanConnectToInternals(gasTank))
                {
                    tanks.Add(gasTank);
                }
            }

            if (tanks.Count > 0)
            {
                tanks.Sort((x, y) => y.Air.TotalMoles.CompareTo(x.Air.TotalMoles));
                return tanks[0];
            }
        }

        return null;
    }
}
