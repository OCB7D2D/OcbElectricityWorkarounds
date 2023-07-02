using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ElectricityWorkarounds : IModApi
{

    // ####################################################################
    // ####################################################################

    public void InitMod(Mod mod)
    {
        Log.Out("OCB Harmony Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    // ####################################################################
    // Don't count down power duration if trigger is still active
    // Only count down after the trigger has been deactivated
    // ####################################################################

    public static float GetTimeByDuration(PowerTrigger.TriggerPowerDurationTypes duration)
    {
        switch (duration)
        {
            case PowerTrigger.TriggerPowerDurationTypes.Always: return -1;
            case PowerTrigger.TriggerPowerDurationTypes.Triggered: return 0.0f;
            case PowerTrigger.TriggerPowerDurationTypes.OneSecond: return 1f;
            case PowerTrigger.TriggerPowerDurationTypes.TwoSecond: return 2f;
            case PowerTrigger.TriggerPowerDurationTypes.ThreeSecond: return 3f;
            case PowerTrigger.TriggerPowerDurationTypes.FourSecond: return 4f;
            case PowerTrigger.TriggerPowerDurationTypes.FiveSecond: return 5f;
            case PowerTrigger.TriggerPowerDurationTypes.SixSecond: return 6f;
            case PowerTrigger.TriggerPowerDurationTypes.SevenSecond: return 7f;
            case PowerTrigger.TriggerPowerDurationTypes.EightSecond: return 8f;
            case PowerTrigger.TriggerPowerDurationTypes.NineSecond: return 9f;
            case PowerTrigger.TriggerPowerDurationTypes.TenSecond: return 10f;
            case PowerTrigger.TriggerPowerDurationTypes.FifteenSecond: return 15f;
            case PowerTrigger.TriggerPowerDurationTypes.ThirtySecond: return 30f;
            case PowerTrigger.TriggerPowerDurationTypes.FourtyFiveSecond: return 45f;
            case PowerTrigger.TriggerPowerDurationTypes.OneMinute: return 60f;
            case PowerTrigger.TriggerPowerDurationTypes.FiveMinute: return 300f;
            case PowerTrigger.TriggerPowerDurationTypes.TenMinute: return 600f;
            case PowerTrigger.TriggerPowerDurationTypes.ThirtyMinute: return 1800f;
            case PowerTrigger.TriggerPowerDurationTypes.SixtyMinute: return 3600f;
        }
        return -1;
    }

    [HarmonyPatch(typeof(PowerTrigger))]
    [HarmonyPatch("set_IsTriggered")]
    public class PowerTrigger_SetIsTriggered
    {
        static void Postfix(PowerTrigger __instance,
            float ___delayStartTime,
            ref bool ___isActive,
            ref float ___lastPowerTime,
            ref float ___powerTime)
        {
            if (___delayStartTime != -1.0) return;
            // Don't apply the new behavior to real switches
            if (__instance.TriggerType == PowerTrigger.TriggerTypes.Switch) return;
            ___isActive = true;
            ___lastPowerTime = Time.time;
            ___powerTime = GetTimeByDuration(
                __instance.TriggerPowerDuration);
        }
    }

    // ####################################################################
    // Don't forcefully remove children if one trigger goes inactive
    // Some child might be another trigger, forming a trigger group,
    // thus if one sub-triggers is active, children stay connected.
    // If any of my parents belonging to my trigger group is active, nothing changed
    // Disconnect all end points that have no active child trigger in their group
    // ####################################################################

    // Check if `child` belongs to the same trigger group as `trigger`
    // Note: changing this implementation alone will probably not change
    // the whole trigger group logic accross the whole game (needs testing)
    public static bool IsSameTriggerGroup(PowerTrigger trigger, PowerTrigger child)
    {
        return child.TriggerType != PowerTrigger.TriggerTypes.Switch
            && trigger.PowerItemType != PowerItem.PowerItemTypes.Timer;
    }

    // Reset parent trigger flags until we find another active child
    // Goes down the whole tree of triggers until one is active itself
    public static void ResetTriggeredByParent(PowerTrigger trigger)
    {
        for (int index = 0; index < trigger.Children.Count; ++index)
        {
            if (trigger.Children[index] is PowerTrigger child)
            {
                child.SetTriggeredByParent(false);
                // If child is still active, it means it is now
                // active on it's own (e.g. `isActive` is true).
                if (!child.IsActive) ResetTriggeredByParent(child);
            }
        }
    }

    [HarmonyPatch(typeof(PowerTrigger))]
    [HarmonyPatch("HandleDisconnectChildren")]
    public class PowerTrigger_HandleDisconnectChildren
    {
        static bool Prefix(PowerTrigger __instance, ref bool ___hasChangesLocal,
            ref bool ___lastTriggered, ref bool ___isTriggered, bool ___parentTriggered)
        {
            // Let the world know that we are no longer active
            // Otherwise the new state will not be persisted
            if (__instance.TileEntity is TileEntityPoweredTrigger te)
            {
                te.Activate(te.IsPowered, te.IsTriggered);
                te.SetModified();
            }

            // Abort now if I'm also parent triggered
            if (___parentTriggered) return false;

            // Reset all parent triggers on children
            // Stops until a child is active itself
            ResetTriggeredByParent(__instance);

            List<PowerItem> disconnects = new List<PowerItem>();
            Queue<PowerTrigger> queue = new Queue<PowerTrigger>();
            queue.Enqueue(__instance);
            while (queue.Count > 0)
            {
                PowerItem child = queue.Dequeue();
                for (int i = 0; i < child.Children.Count; ++i)
                {
                    // Check if child is another power trigger
                    if (child.Children[i] is PowerTrigger trigger)
                    {
                        // Check if child trigger is in the same group
                        if (IsSameTriggerGroup(__instance, trigger))
                        {
                            // If child trigger is active the whole group
                            // downstream is active, so skip it completely
                            if (trigger.IsActive) continue;
                            // Otherwise check new child trigger
                            queue.Enqueue(trigger);
                        }
                        else
                        {
                            // Child has broken the group
                            disconnects.Add(trigger);
                        }
                    }
                    // Otherwise group is broken
                    else
                    {
                        disconnects.Add(child.Children[i]);
                    }
                }
            }

            foreach (PowerItem item in disconnects)
            {
                // item.HandlePowerUpdate(false);
                if (item is PowerTrigger trigger)
                {
                    trigger.HandleDisconnectChildren();
                }
                else
                {
                    item.HandleDisconnect();
                }
            }

            return false;
        }
    }

    // ####################################################################
    // Pressure plates, trip wires and motion sensors exhibit a strange behavior
    // when duration is set to `triggered` with a `startDelay`. If a target is
    // standing on the plate/wire, the power should go on after the delay and
    // once the target steps of the plate/wire, it should stay on for the whole
    // duration; in case of `triggered` it should instantly turn off. If the
    // target steps off the plate/write before the delay has passed, power
    // would still be turned on after the delay, but never turned off...
    // ####################################################################

    [HarmonyPatch(typeof(PowerTrigger))]
    [HarmonyPatch("CachedUpdateCall")]
    public class PowerTrigger_CachedUpdateCall
    {
        static void Prefix(PowerTrigger __instance,
            bool ___isTriggered, ref float ___delayStartTime)
        {
            // Check if trigger is being deactivated and if trigger duration is set to `triggered` (instant on/off)
            // In that case it doesn't make sense to wait for the start delay, since it should instantly turn off again
            // Unfortunately in the original game this edge-cause would cause the power to be on permanently (this fixes it).
            if (___isTriggered == false && __instance.TriggerPowerDuration == PowerTrigger.TriggerPowerDurationTypes.Triggered)
            {
                ___delayStartTime = -1;
            }
        }
    }

    // ####################################################################
    // ####################################################################

    static bool PM_Loaded = false;

    [HarmonyPatch(typeof(PowerManager))]
    [HarmonyPatch(MethodType.Constructor)]
    public class PowerManager_CTOR
    {
        static void Prefix()
        {
            PM_Loaded = false;
        }
    }

    [HarmonyPatch(typeof(PowerManager))]
    [HarmonyPatch("LoadPowerManager")]
    public class PowerManager_LoadPowerManager
    {
        static void Postfix()
        {
            PM_Loaded = true;
        }
    }

    [HarmonyPatch(typeof(PowerManager))]
    [HarmonyPatch("savePowerDataThreaded")]
    public class PowerManager_savePowerDataThreaded
    {
        static bool Prefix(ref int __result)
        {
            __result = -1;
            return PM_Loaded;
        }
    }

    // ####################################################################
    // ####################################################################

}
