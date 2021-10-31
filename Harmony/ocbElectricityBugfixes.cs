using DMT;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

public class OcbElectricityBugfixes
{

    // Entry class for Harmony patching
    public class OcbElectricityBugfixes_Init : IHarmony
    {
        public void Start()
        {
            Debug.Log("Loading OCB Electricity Bugfixes Patch: " + GetType().ToString());
            var harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    // Don't count down power duration if trigger is still active
    // Only count down after the trigger has been deactivated
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
            if (__instance.TriggerType != PowerTrigger.TriggerTypes.Switch)
            {
                if (___delayStartTime == -1.0)
                {
                    ___isActive = true;
                    ___lastPowerTime = Time.time;
                    // Had to copy `SetupDurationTime` due to protection
                    // This way we keep the patch EAC compatible (I guess)
                    switch (__instance.TriggerPowerDuration)
                    {
                        case PowerTrigger.TriggerPowerDurationTypes.Always:
                            ___powerTime = -1f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.Triggered:
                            ___powerTime = 0.0f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.OneSecond:
                            ___powerTime = 1f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.TwoSecond:
                            ___powerTime = 2f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.ThreeSecond:
                            ___powerTime = 3f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.FourSecond:
                            ___powerTime = 4f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.FiveSecond:
                            ___powerTime = 5f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.SixSecond:
                            ___powerTime = 6f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.SevenSecond:
                            ___powerTime = 7f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.EightSecond:
                            ___powerTime = 8f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.NineSecond:
                            ___powerTime = 9f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.TenSecond:
                            ___powerTime = 10f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.FifteenSecond:
                            ___powerTime = 15f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.ThirtySecond:
                            ___powerTime = 30f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.FourtyFiveSecond:
                            ___powerTime = 45f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.OneMinute:
                            ___powerTime = 60f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.FiveMinute:
                            ___powerTime = 300f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.TenMinute:
                            ___powerTime = 600f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.ThirtyMinute:
                            ___powerTime = 1800f;
                            break;
                        case PowerTrigger.TriggerPowerDurationTypes.SixtyMinute:
                            ___powerTime = 3600f;
                            break;
                    }
                }
            }
        }
    }

    // Don't forcefully remove children if one trigger goes inactive
    // Some child might be another trigger, forming a trigger group,
    // thus if one sub-triggers is active, children stay connected.
    [HarmonyPatch(typeof(PowerTrigger))]
    [HarmonyPatch("HandleDisconnectChildren")]
    public class PowerTrigger_HandleDisconnectChildren
    {
        static bool Prefix(PowerTrigger __instance)
        {
            // Trigger Switches do not contribute to trigger groups in vanilla
            if (__instance.TriggerType == PowerTrigger.TriggerTypes.Switch) {
                return true;
            }
            // Make sure no parent is triggered
            if (__instance.Parent != null) {
                if (__instance.Parent is PowerTrigger upstream) {
                    if (upstream.TriggerType != PowerTrigger.TriggerTypes.Switch) {
                        if (upstream.IsActive) {
                            return false;
                        }
                    }
                }
            }
            for (int index = 0; index < __instance.Children.Count; ++index)
            {
                // If child is another trigger, only disconnect if not active
                if (__instance.Children[index] is PowerTrigger trigger)
                {
                    trigger.SetTriggeredByParent(__instance.IsActive);
                    if (trigger.TriggerType == PowerTrigger.TriggerTypes.Switch) {
                        // We are not active, so Switches will break the group
                    }
                    else if (trigger.IsActive) {
                        continue;
                    }
                    trigger.HandleDisconnectChildren();
                }
                else {
                    __instance.Children[index].HandleDisconnect();
                }
            }
            // Disable power for this instance?
            // Get better results without this?
            // __instance.HandlePowerUpdate(false);
            // Fully replace implementation
            return false;
        }
    }

    // Pressure plates, trip wires and motion sensors exhibit a strange behavior
    // when duration is set to `triggered` with a `startDelay`. If a target is
    // standing on the plate/wire, the power should go on after the delay and
    // once the target steps of the plate/wire, it should stay on for the whole
    // duration; in case of `triggered` it should instantly turn off. If the
    // target steps off the plate/write before the delay has passed, power
    // would still be turned on after the delay, but never turned off...
    [HarmonyPatch(typeof(PowerTrigger))]
    [HarmonyPatch("CachedUpdateCall")]
    public class PowerTrigger_CachedUpdateCall
    {
        static void Prefix(PowerTrigger __instance,
            bool ___isTriggered, ref float ___delayStartTime)
        {
            // Check if trigger is being deactivated ans if trigger duration is set to `triggered` (instant on/off)
            // In that case it doesn't make sense to wait for the start delay, since it should instantly turn off again
            // Unfortunately in the original game this edge-cause would cause the power to be on permanently (this fixes it).
            if (___isTriggered == false && __instance.TriggerPowerDuration == PowerTrigger.TriggerPowerDurationTypes.Triggered)
            {
                ___delayStartTime = -1;
            }
        }
    }

}
