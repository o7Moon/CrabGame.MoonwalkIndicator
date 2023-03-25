using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace MoonwalkIndicator
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        const float MOONWALK_SPEED_THRESHOLD = 19.5f;// its actually 20 but this value seems have less false positives so eh 
        static float lastHorizontalSpeed = 0;
        static Transform velocityIndicator;
        static ConfigEntry<bool> enableFlash;
        static ConfigEntry<bool> enableVelocity;
        static ConfigEntry<bool> enableWidth;
        static Plugin Instance;
        public override void Load()
        {
            Instance = this;
            enableFlash = Config.Bind<bool>(
                "Features",
                "Damage Flash On Dropped Moonwalk",
                true
            );
            enableVelocity = Config.Bind<bool>(
                "Features",
                "Velocity Dot",
                true,
                "Shows the angle of your velocity, useful for understanding moonwalking"
            );
            enableWidth = Config.Bind<bool>(
                "Features",
                "Dot Scaling",
                true,
                "Makes the velocity dot scale so that it's width is equal to the minimum angle you need in order to moonwalk. \nif enabled, this should put your crosshair near the edge of the dot's width while moonwalking"
            );
            SceneManager.sceneLoaded += (UnityAction<Scene, LoadSceneMode>) OnSceneLoad;
            Harmony.CreateAndPatchAll(typeof(Plugin));
            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        public static void OnSceneLoad(Scene scene, LoadSceneMode mode){
            Instance.Config.Reload();
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
            Vector2 look = new Vector2(look3.x,look3.z).normalized;
            Vector3 vel3 = GetPlayerBody().velocity;
            Vector2 vel = new Vector2(vel3.x,vel3.z).normalized;
            return Vector2.SignedAngle(vel,look);
        }
        public static float AngleNeededToAvoidSlowdown(){
            Vector3 velocity = GetPlayerBody().velocity;
            Vector2 horizontal = new Vector2(velocity.x, velocity.z);
            float speed = horizontal.magnitude;
            if (speed < 18.4f) return 0; // arcsin would give negative value below this threshold
            return 45 - (Mathf.Asin(13f/speed) * Mathf.Rad2Deg);// given the speed and maximum speed, find the angle between velocity and input dir where one axis of relative velocity is exactly at max speed
        }
        [HarmonyPatch(typeof(PlayerMovement),nameof(PlayerMovement.Update))]
        [HarmonyPostfix]
        public static void PostPlayerUpdate(PlayerMovement __instance){
            if (enableFlash.Value){
                var horizontal = __instance.GetComponent<Rigidbody>().velocity;
                horizontal.y = 0;
                float horizontalSpeed = horizontal.magnitude;
                if (__instance.grounded && !(__instance.IsCrouching() || __instance.IsSliding() || __instance.IsDead()) && lastHorizontalSpeed > MOONWALK_SPEED_THRESHOLD && horizontalSpeed <= MOONWALK_SPEED_THRESHOLD){
                    DamageVignette.Damage();
                }
                lastHorizontalSpeed = horizontalSpeed;
            }
            velocityIndicator.gameObject.SetActive(enableVelocity.Value);
            if (enableWidth.Value){
                velocityIndicator.localPosition = new Vector2(AngleFromLookToVelocity()*4f,0);
                float scale = AngleNeededToAvoidSlowdown();
                scale = scale * 4f;
                velocityIndicator.localScale = new Vector2(scale,5);
            } else {
                velocityIndicator.localScale = new Vector2(5,5);
            }
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
            velocityIndicator.localScale = new Vector2(0,0);
        }
    }
}
