using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoonwalkIndicator
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        const float MOONWALK_SPEED_THRESHOLD = 19.5f;// its actually 20 but this value seems have less false positives for some reason 
        static float lastHorizontalSpeed = 0;
        static Transform velocityIndicator;
        public override void Load()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        public static Rigidbody GetPlayerBody(){
            return PlayerInput.Instance.GetComponent<Rigidbody>();
        }
        public static Transform GetPlayerOrientation(){
            return PlayerInput.Instance.transform.FindChild("Orientation");
        }
        public static Vector3 GetInputDir(){
            Transform o = GetPlayerOrientation();
            PlayerInput i = PlayerInput.Instance;
            return o.forward * i.y + o.right * i.x;
        }
        public static float AngleFromLookToVelocity(){
            Vector3 look3 = GetInputDir();
            //Debug.Log(look3.ToString());
            Vector2 look = new Vector2(look3.x,look3.z).normalized;
            Vector3 vel3 = GetPlayerBody().velocity;
            //Debug.Log(vel3.ToString());
            Vector2 vel = new Vector2(vel3.x,vel3.z).normalized;
            return Vector2.SignedAngle(vel,look);
        }
        [HarmonyPatch(typeof(PlayerMovement),nameof(PlayerMovement.Update))]
        [HarmonyPostfix]
        public static void PostPlayerUpdate(PlayerMovement __instance){
            var horizontal = __instance.GetComponent<Rigidbody>().velocity;
            horizontal.y = 0;
            float horizontalSpeed = horizontal.magnitude;
            if (__instance.grounded && !(__instance.IsCrouching() || __instance.IsSliding() || __instance.IsDead()) && lastHorizontalSpeed > MOONWALK_SPEED_THRESHOLD && horizontalSpeed <= MOONWALK_SPEED_THRESHOLD){
                DamageVignette.Damage();
            }
            lastHorizontalSpeed = horizontalSpeed;
            velocityIndicator.localPosition = new Vector2(AngleFromLookToVelocity()*4f,0);
            velocityIndicator.localScale = new Vector2(5,5);
            //typeof(MonoBehaviourPublicGaroloGaObInCacachGaUnique)
        }
        [HarmonyPatch(typeof(GameUI),nameof(GameUI.Start))]
        [HarmonyPostfix]
        public static void PostGameUIStart(GameUI __instance){
            Transform crosshair = __instance.transform.FindChild("Crosshair");
            GameObject dot = crosshair.FindChild("Dot").gameObject;
            GameObject velocityDot = GameObject.Instantiate(dot);
            velocityDot.transform.SetParent(crosshair);
            velocityDot.SetActive(true);
            velocityDot.GetComponent<RawImage>().color = new Color(0.3f,0.3f,1,1);
            velocityIndicator = velocityDot.transform;
            velocityIndicator.localScale = new Vector2(3,5);
        }
    }
}
