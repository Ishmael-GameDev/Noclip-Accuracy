using Modding;
using UnityEngine;
using System.Collections;
using GlobalEnums;
using InControl;
using Newtonsoft.Json;
using Modding.Converters;
using Satchel;
using System.Reflection;
using System.IO;
using System.Linq;

namespace NoclipAccuracy
{
    public class NoclipActionSet : PlayerActionSet
    {
        public PlayerAction Reset;

        public NoclipActionSet()
        {
            Reset = CreatePlayerAction("Reset Counter");
            Reset.AddDefaultBinding(Key.R);
        }
    }

    public class GlobalSettings
    {
        public bool ShowCounter = true;
        public int PositionIndex = 0;
        public float BurstDelay = 0.5f;

        [JsonProperty]
        [JsonConverter(typeof(PlayerActionSetConverter))]
        public NoclipActionSet Keybinds = new();
    }

    public class NoclipAccuracy : Mod, IGlobalSettings<GlobalSettings>, ICustomMenuMod
    {
        public override string GetVersion() => "1.4.2";

        public static GlobalSettings Settings { get; set; } = new GlobalSettings();
        public static NoclipAccuracy Instance;

        public void OnLoadGlobal(GlobalSettings s) => Settings = s;
        public GlobalSettings OnSaveGlobal() => Settings;

        // STATE
        private int hits = 0;
        private bool lastAtBench;
        private float lastHitTime = -999f;
        private bool inBurst = false;

        private GameObject hud;
        private Vector3 origpos;

        private Sprite _hudSprite;

        // fullscreen flash
        private GameObject _flashOverlay;

        public override void Initialize()
        {
            Instance = this;

            LoadSprite();

            ModHooks.TakeDamageHook += OnTakeDamageHook;
            On.HeroController.Awake += HeroAwake;
            On.HeroController.Update += HeroUpdate;
        }

        // LOAD PNG
        private void LoadSprite()
        {
            var resName = Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .First(x => x.EndsWith("death.png"));

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);

            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);

            var tex = new Texture2D(2, 2);
            tex.LoadImage(buffer);

            _hudSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }

        // RESET
        private void HeroUpdate(On.HeroController.orig_Update orig, HeroController self)
        {
            orig(self);

            if (Settings.Keybinds.Reset.WasPressed)
            {
                hits = 0;
                UpdateHUD();
            }
            bool nowAtBench = PlayerData.instance != null && PlayerData.instance.atBench;

            if (nowAtBench && !lastAtBench)
            {
                hits = 0;
                UpdateHUD();
            }

            lastAtBench = nowAtBench;
        }

        // DAMAGE
        private int OnTakeDamageHook(ref int hazardType, int damage)
        {
            if (damage <= 0) return damage;
            if (hazardType <= (int)HazardType.SPIKES) return damage;

            float now = Time.realtimeSinceStartup;

            if (inBurst)
            {
                lastHitTime = now;
                return damage;
            }

            inBurst = true;
            HeroController.instance?.StartCoroutine(BurstWatcher());

            TriggerFlash();

            lastHitTime = now;
            hits++;
            UpdateHUD();

            return damage;
        }

        private IEnumerator BurstWatcher()
        {
            while (true)
            {
                yield return null;

                if (Time.realtimeSinceStartup - lastHitTime > Settings.BurstDelay)
                    break;
            }

            inBurst = false;
        }

        // FLASH
        private void TriggerFlash()
        {
            if (_flashOverlay == null)
                CreateFlashOverlay();

            HeroController.instance?.StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _flashOverlay.SetActive(true);

            var img = _flashOverlay.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(1f, 0f, 0f, 0.1f);

            yield return new WaitForSeconds(0.2f);

            _flashOverlay.SetActive(false);
        }

        private void CreateFlashOverlay()
        {
            var canvas = new GameObject("HitFlashCanvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var imgObj = new GameObject("Flash");
            imgObj.transform.SetParent(canvas.transform);

            var img = imgObj.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(1, 0, 0, 0);

            var rect = img.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _flashOverlay = imgObj;
            _flashOverlay.SetActive(false);

            Object.DontDestroyOnLoad(canvas);
        }

        // HUD
        private void HeroAwake(On.HeroController.orig_Awake orig, HeroController self)
        {
            orig(self);

            var hudCanvas = GameObject.Find("_GameCameras")
                .FindGameObjectInChildren("HudCamera")
                .FindGameObjectInChildren("Hud Canvas");

            var geo = GameManager.instance.inventoryFSM.gameObject
                .FindGameObjectInChildren("Geo");

            origpos = geo.transform.position;

            CreateHUD(geo, hudCanvas);
        }

        private void CreateHUD(GameObject prefab, GameObject parent)
        {
            hud = Object.Instantiate(prefab, parent.transform, true);

            hud.transform.position = origpos + GetOffset();

            hud.SetActive(Settings.ShowCounter);

            // TEXT
            var display = hud.GetComponent<DisplayItemAmount>();
            display.textObject.fontSize = 4;
            display.textObject.text = "0 hits";

            // SPRITE REPLAC
            var sr = hud.GetComponent<SpriteRenderer>();
            if (sr != null && _hudSprite != null)
                sr.sprite = _hudSprite;

            UpdateHUD();
        }

        private void UpdateHUD()
        {
            if (hud == null || !Settings.ShowCounter) return;

            hud.GetComponent<DisplayItemAmount>()
                .textObject.text = $"{hits} hits";
        }

        public void Redraw()
        {
            if (hud == null) return;

            hud.SetActive(Settings.ShowCounter);

            if (!Settings.ShowCounter) return;

            hud.transform.position = origpos + GetOffset();
            UpdateHUD();
        }

        private Vector3 GetOffset()
        {
            return Settings.PositionIndex switch
            {
                0 => new Vector3(20f, 13f, 0f),
                1 => new Vector3(8f, 13f, 0f),
                2 => new Vector3(2f, 11f, 0f),
                _ => new Vector3(20f, 13f, 0f)
            };
        }

        public bool ToggleButtonInsideMenu => true;

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
            => ConfigurationScreen.GetScreen(modListMenu, Settings);
    }
}