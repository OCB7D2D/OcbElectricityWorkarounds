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
            Debug.Log("Loading Electricity Bugfixes: " + GetType().ToString());
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
            for (int index = 0; index < __instance.Children.Count; ++index)
            {
                // If child is another trigger, only disconnect if not active
                if (__instance.Children[index] is PowerTrigger trigger)
                {
                    if (trigger.IsActive) continue;
                    trigger.HandleDisconnectChildren();
                }
                else
                {
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

}
