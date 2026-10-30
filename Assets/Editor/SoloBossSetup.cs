using System;
using System.Linq;
using MagicTouch.Gameplay;
using MagicTouch.Juice;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Manual, repeatable boss wiring. Rebuilds the Ember Drake data assets and the boss objects/HUD layers in the open scene.
    /// Never runs on reload; only touches the boss, the boss bar, the music director and the clip assignments it names.
    /// </summary>
    public static class SoloBossSetup
    {
        private const string Art = "Assets/Sprites/bosses/castle/ember_drake_tyrant/";
        private const string Manifest = Art + "ember_drake_tyrant_clips.json";
        private const string Animations = Art + "ember_drake_tyrant_clips_Animations.asset";
        private const string SkillFolder = Art + "Skills";
        private const string DefinitionPath = Art + "EmberDrake.asset";
        private const string Audio = "Assets/Audio/";

        private const string DullahanArt = "Assets/Sprites/bosses/castle/dullahan_guard/";
        private const string DullahanManifest = DullahanArt + "dullahan_guard_clips.json";
        private const string DullahanAnimations = DullahanArt + "dullahan_guard_clips_Animations.asset";
        private const string DullahanSkillFolder = DullahanArt + "Skills";
        private const string DullahanDefinitionPath = DullahanArt + "DullahanGuard.asset";

        [MenuItem("Tools/Magic Touch/Boss/Rebuild Ember Drake Wiring (manual)")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install in Edit Mode.");
            var game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (game == null) throw new InvalidOperationException("GameManager is required.");
            var spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null) throw new InvalidOperationException("EnemySpawner is required.");

            var set = BossAnimationImporter.Build(Manifest, Animations);
            var definition = BuildDefinition(set);
            var encounter = Component<SoloBossEncounter>(game.gameObject);

            // The old platform and its 60+ stone blocks are gone: the boss hovers.
            var oldArena = FindRoot("EmberDrakeArena");
            if (oldArena != null) Undo.DestroyObjectImmediate(oldArena);

            var root = NewObject("EmberDrake", null);
            root.transform.position = Vector3.zero;
            var caster = Component<BossSkillCaster>(root);
            var entrance = Component<BossEntrance>(root);
            var body = NewObject("EnemyBody", root.transform);
            var renderer = Component<SpriteRenderer>(body);
            var wizard = game.playerWizardSprite != null ? game.playerWizardSprite : GameObject.Find("PlayerWizard").GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = wizard.sortingLayerID;
            renderer.sortingOrder = definition.SortingOrder;
            renderer.sprite = set.Get("idle").frames[0];
            var visual = Component<BossVisual>(body);
            var v = new SerializedObject(visual);
            Reference(v, "_body", renderer);
            Reference(v, "_animations", set);
            v.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);

            var hud = BuildHud(game, set, out var fill, out var fillImage, out var overlay, out var overlayImage, out var label, out var marker);

            var config = new SerializedObject(encounter);
            Reference(config, "_boss", definition);
            Reference(config, "_spawner", spawner);
            Reference(config, "_bossRoot", root);
            Reference(config, "_visual", visual);
            Reference(config, "_caster", caster);
            Reference(config, "_entrance", entrance);
            Reference(config, "_hud", hud);
            Reference(config, "_fill", fill);
            Reference(config, "_fillImage", fillImage);
            Reference(config, "_label", label);
            Reference(config, "_marker", marker);
            var overlays = config.FindProperty("_overlayFills");
            overlays.arraySize = 1;
            overlays.GetArrayElementAtIndex(0).objectReferenceValue = overlay;
            var images = config.FindProperty("_overlayImages");
            images.arraySize = 1;
            images.GetArrayElementAtIndex(0).objectReferenceValue = overlayImage;
            config.ApplyModifiedPropertiesWithoutUndo();
            hud.SetActive(false);

            WireMusic(game);

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Selection.activeGameObject = game.gameObject;
            Debug.Log("[SoloBossSetup] Ember Drake rebuilt: hover boss, red/green bar layers, music director.");
        }

        private static BossDefinition BuildDefinition(BossAnimationSet set)
        {
            if (!AssetDatabase.IsValidFolder(SkillFolder)) AssetDatabase.CreateFolder(Art.TrimEnd('/'), "Skills");
            // Content heights are the share of the 128 px effect cell the picture fills (measured from the sheet).
            var fireball = Skill("Fireball", "skill_a", 6, BossSkillKind.Projectile, "fireball_down", 0, 1f, new Vector2(0f, .7f), .4f, 0, .8f);
            var spike = Skill("GroundSpike", "skill_b", 6, BossSkillKind.GroundSpike, "ground_spike", 2, 1.5f, new Vector2(0f, .49f), 0f, 0, .78f);
            var clawLeft = Skill("ClawLeft", "skill_claw_left", 4, BossSkillKind.Slash, "claw_slash", 1, 1.6f, Vector2.zero, 0f, (int)BossSlashSides.Left, .7f);
            var clawRight = Skill("ClawRight", "skill_claw_right", 4, BossSkillKind.Slash, "claw_slash", 1, 1.6f, Vector2.zero, 0f, (int)BossSlashSides.Right, .7f);
            var slam = Skill("SlamDown", "skill_slam_down", 6, BossSkillKind.Slash, "claw_slash", 1, 1.6f, Vector2.zero, 0f,
                (int)(BossSlashSides.Left | BossSlashSides.Right), .7f);

            var def = Asset<BossDefinition>(DefinitionPath);
            var so = new SerializedObject(def);
            so.FindProperty("_displayName").stringValue = "EMBER DRAKE TYRANT";
            so.FindProperty("_animations").objectReferenceValue = set;
            so.FindProperty("_minionText").stringValue = "ROAD TO THE TYRANT";
            so.FindProperty("_introText").stringValue = "THE TYRANT APPROACHES!";
            so.FindProperty("_defeatedText").stringValue = "TYRANT DEFEATED!";
            so.FindProperty("_minionsBeforeBoss").intValue = 20; // unused: the stage runs on _surviveSeconds
            so.FindProperty("_surviveSeconds").floatValue = 60f;
            so.FindProperty("_refillSeconds").floatValue = 1.2f;
            so.FindProperty("_entranceBeforeRefill").boolValue = true;
            so.FindProperty("_damagePerBalloon").intValue = 10;
            so.FindProperty("_balloonsAround").intValue = 5;
            so.FindProperty("_balloonRerollSeconds").floatValue = 6f;
            so.FindProperty("_minionBarColor").colorValue = new Color(.45f, .78f, .22f);
            so.FindProperty("_hurtClip").stringValue = "hurt";
            so.FindProperty("_deathClip").stringValue = "death";
            so.FindProperty("_hoverViewport").vector2Value = new Vector2(.5f, .70f);
            so.FindProperty("_scale").floatValue = 1.5f;
            so.FindProperty("_sortingOrder").intValue = 2;
            so.FindProperty("_balloonLaneX").floatValue = .17f;
            so.FindProperty("_balloonLaneY").vector2Value = new Vector2(.36f, .62f);
            so.FindProperty("_hasEntrance").boolValue = true;
            so.FindProperty("_whirlwindClip").stringValue = "whirlwind";
            so.FindProperty("_pillarClip").stringValue = "pillar";
            so.FindProperty("_pillarOffset").vector2Value = new Vector2(0f, -.1f);
            so.FindProperty("_whirlwindScaleMultiple").floatValue = 1.4f;
            so.FindProperty("_pillarWidthFactor").floatValue = .54f;
            so.FindProperty("_pillarClearance").floatValue = 1.2f;
            so.FindProperty("_bodyHalfHeight").floatValue = 1.94f;
            so.FindProperty("_pillarScaleMultiple").floatValue = 2f;

            so.FindProperty("_roarClip").stringValue = "roar";
            so.FindProperty("_roarSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "BossRoar_EmberDrake.ogg");
            var segments = so.FindProperty("_segments");
            segments.arraySize = 2;
            Segment(segments.GetArrayElementAtIndex(0), "Red", 100, new Color(.94f, .24f, .12f), "idle", 4.2f, clawLeft, clawRight, slam);
            Segment(segments.GetArrayElementAtIndex(1), "Green", 200, new Color(.45f, .78f, .22f), "idle_cracked", 3.6f,
                fireball, spike, clawLeft, clawRight, slam);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return def;
        }


        [MenuItem("Tools/Magic Touch/Boss/Rebuild Dullahan Guard Wiring (manual)")]
        public static void InstallDullahan()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install in Edit Mode.");
            var game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (game == null) throw new InvalidOperationException("GameManager is required.");
            var encounter = game.GetComponent<SoloBossEncounter>();
            if (encounter == null) throw new InvalidOperationException("Run 'Rebuild Ember Drake Wiring (manual)' first: the encounter is missing.");

            var set = BossAnimationImporter.Build(DullahanManifest, DullahanAnimations);
            var definition = BuildDullahanDefinition(set);

            var root = NewObject("DullahanGuard", null);
            root.transform.position = Vector3.zero;
            var caster = Component<BossSkillCaster>(root);
            var entrance = Component<DullahanEntrance>(root);
            var body = NewObject("EnemyBody", root.transform);
            var renderer = Component<SpriteRenderer>(body);
            var wizard = game.playerWizardSprite != null ? game.playerWizardSprite : GameObject.Find("PlayerWizard").GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = wizard.sortingLayerID;
            renderer.sortingOrder = definition.SortingOrder;
            renderer.sprite = set.Get("idle").frames[0];
            var visual = Component<BossVisual>(body);
            var v = new SerializedObject(visual);
            Reference(v, "_body", renderer);
            Reference(v, "_animations", set);
            v.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);

            // The mini boss is the first stage of the run; the Ember Drake stays the main (last) boss.
            var config = new SerializedObject(encounter);
            var stages = config.FindProperty("_preludeBosses");
            stages.arraySize = 1;
            var stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("boss").objectReferenceValue = definition;
            stage.FindPropertyRelative("root").objectReferenceValue = root;
            stage.FindPropertyRelative("visual").objectReferenceValue = visual;
            stage.FindPropertyRelative("caster").objectReferenceValue = caster;
            stage.FindPropertyRelative("entrance").objectReferenceValue = entrance;
            config.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Selection.activeGameObject = root;
            Debug.Log("[SoloBossSetup] Dullahan Guard wired as the first stage (90 s wave, 200 HP red bar, stone stage entrance).");
        }

        private static BossDefinition BuildDullahanDefinition(BossAnimationSet set)
        {
            // Effect cells are 512 px and the drawn picture fills ~94% of them (measured from the effects sheet).
            var cross = Skill("CrossSlash", "attack_cross_slash", 4, BossSkillKind.Slash, "cross_slash_fx", 1, 1f, Vector2.zero, 0f,
                (int)BossSlashSides.Left, .94f, DullahanSkillFolder);
            var helmet = Skill("HelmetToss", "attack_helmet_toss", 6, BossSkillKind.GroundSpike, "helmet_flame_burst_fx", 2, 1f,
                new Vector2(0f, .3f), 0f, 0, .94f, DullahanSkillFolder);

            var def = Asset<BossDefinition>(DullahanDefinitionPath);
            var so = new SerializedObject(def);
            so.FindProperty("_displayName").stringValue = "DULLAHAN GUARD";
            so.FindProperty("_animations").objectReferenceValue = set;
            so.FindProperty("_minionText").stringValue = "ROAD TO DULLAHAN";
            so.FindProperty("_introText").stringValue = "THE DREAD GENERAL DESCENDS!";
            so.FindProperty("_defeatedText").stringValue = "DULLAHAN DEFEATED!";
            so.FindProperty("_surviveSeconds").floatValue = 90f;
            so.FindProperty("_refillSeconds").floatValue = 3f;
            so.FindProperty("_entranceBeforeRefill").boolValue = false;
            so.FindProperty("_damagePerBalloon").intValue = 10;
            so.FindProperty("_balloonsAround").intValue = 5;
            so.FindProperty("_balloonRerollSeconds").floatValue = 6f;
            so.FindProperty("_minionBarColor").colorValue = new Color(.45f, .78f, .22f);
            so.FindProperty("_hurtClip").stringValue = "hurt";
            so.FindProperty("_deathClip").stringValue = "death";
            so.FindProperty("_hoverViewport").vector2Value = new Vector2(.5f, .55f);
            so.FindProperty("_scale").floatValue = 1.5f;
            so.FindProperty("_sortingOrder").intValue = 3;
            so.FindProperty("_balloonLaneX").floatValue = .17f;
            so.FindProperty("_balloonLaneY").vector2Value = new Vector2(.36f, .62f);
            so.FindProperty("_feetOffset").floatValue = 0f;       // the sheets are pivoted at the feet (row 480 of 512)
            so.FindProperty("_bodyHalfHeight").floatValue = 1.74f; // drawn from row 132 to 480
            so.FindProperty("_bodyWidth").floatValue = 3.4f;
            so.FindProperty("_platformScale").floatValue = 1.5f;
            so.FindProperty("_platformStand").floatValue = .5f;   // feet toward the front of the stage top, not on its back edge
            so.FindProperty("_dustScaleMultiple").floatValue = 1.3f;
            so.FindProperty("_hasEntrance").boolValue = true;
            so.FindProperty("_whirlwindClip").stringValue = "";
            so.FindProperty("_pillarClip").stringValue = "";
            so.FindProperty("_roarClip").stringValue = "";
            so.FindProperty("_roarSound").objectReferenceValue = null;
            var segments = so.FindProperty("_segments");
            segments.arraySize = 1;
            Segment(segments.GetArrayElementAtIndex(0), "Red", 200, new Color(.94f, .24f, .12f), "idle", 4.2f, cross, helmet);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void Segment(SerializedProperty p, string name, int health, Color color, string rest, float interval,
            params BossSkillDefinition[] skills)
        {
            p.FindPropertyRelative("name").stringValue = name;
            p.FindPropertyRelative("health").intValue = health;
            p.FindPropertyRelative("barColor").colorValue = color;
            p.FindPropertyRelative("restClip").stringValue = rest;
            p.FindPropertyRelative("castInterval").floatValue = interval;
            var list = p.FindPropertyRelative("skills");
            list.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        }

        private static BossSkillDefinition Skill(string name, string clip, int impact, BossSkillKind kind, string fx, int fxHit,
            float fxScale, Vector2 offset, float columnChance, int sides, float contentHeight,
            string folder = SkillFolder, float telegraph = 2.4f) // green .9 + yellow 1.1 + red .4
        {
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(folder.Substring(0, folder.LastIndexOf('/')), "Skills");
            var skill = Asset<BossSkillDefinition>($"{folder}/{name}.asset");
            var so = new SerializedObject(skill);
            so.FindProperty("_bossClip").stringValue = clip;
            so.FindProperty("_impactFrame").intValue = impact;
            so.FindProperty("_telegraphSeconds").floatValue = telegraph;
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_fxClip").stringValue = fx;
            so.FindProperty("_slashSides").intValue = sides;
            so.FindProperty("_fxHitFrame").intValue = fxHit;
            so.FindProperty("_fxScale").floatValue = fxScale;
            so.FindProperty("_playerHeightMultiple").floatValue = 1.5f; // every skill effect is 1.5x as tall as the player
            so.FindProperty("_contentHeight").floatValue = contentHeight;
            so.FindProperty("_fxOffset").vector2Value = offset;
            so.FindProperty("_fxFrameSeconds").floatValue = .08f;
            so.FindProperty("_columnChance").floatValue = columnChance;
            so.FindProperty("_damage").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static GameObject BuildHud(GameManager game, BossAnimationSet set, out RectTransform fill, out Image fillImage,
            out RectTransform overlay, out Image overlayImage, out TMP_Text label, out RectTransform marker)
        {
            var hud = NewObject("SoloBossHUD", null, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 105;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var panel = Rect("EncounterBar", hud.transform, new Vector2(.29f, .865f), new Vector2(.71f, .95f));
            Solid(panel.gameObject, new Color(.09f, .045f, .12f, .96f));
            var text = Rect("Label", panel, new Vector2(.035f, .49f), new Vector2(.965f, .96f)).gameObject;
            var tmp = Component<TextMeshProUGUI>(text);
            tmp.font = game.scoreText != null ? game.scoreText.font : TMP_Settings.defaultFontAsset;
            tmp.fontSize = 23;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 12;
            tmp.fontSizeMax = 23;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, .84f, .48f);
            tmp.raycastTarget = false;
            tmp.text = "ROAD TO THE TYRANT   0 / 20";
            label = tmp;

            var track = Rect("Track", panel, new Vector2(.04f, .16f), new Vector2(.96f, .45f));
            Solid(track.gameObject, new Color(.22f, .13f, .18f));
            fill = Rect("Fill", track, Vector2.zero, new Vector2(0, 1));
            fillImage = Solid(fill.gameObject, new Color(.45f, .78f, .22f));
            overlay = Rect("RedFill", track, Vector2.zero, new Vector2(0, 1));
            overlayImage = Solid(overlay.gameObject, new Color(.94f, .24f, .12f));
            overlay.SetSiblingIndex(fill.GetSiblingIndex() + 1);
            for (int i = 1; i < 10; i++)
            {
                var tick = Rect("Milestone" + i, track, new Vector2(i * .1f, 0), new Vector2(i * .1f, 1));
                tick.sizeDelta = new Vector2(2, 0);
                Solid(tick.gameObject, new Color(.1f, .06f, .09f, .55f));
            }
            marker = Rect("BossFlag", track, new Vector2(0, .5f), new Vector2(0, .5f));
            marker.sizeDelta = new Vector2(48, 48);
            var flag = Solid(marker.gameObject, Color.white);
            flag.sprite = set.Get("idle").frames[0];
            flag.preserveAspect = true;
            return hud;
        }

        private static void WireMusic(GameManager game)
        {
            var menu = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "menu.ogg");
            var creep = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "CombatCastleMusic.wav");
            var appear = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "boss_appear.ogg");
            var combat = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "boss_combat.ogg");
            var victory = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "GameVictory.ogg");
            if (menu == null || creep == null || appear == null || combat == null || victory == null)
                throw new InvalidOperationException("Music .ogg files are missing from Assets/Audio (let Unity import them first).");

            var director = Component<MusicDirector>(game.gameObject);
            var d = new SerializedObject(director);
            Reference(d, "_menu", menu);
            Reference(d, "_creep", creep);
            Reference(d, "_bossAppear", appear);
            Reference(d, "_bossCombat", combat);
            d.ApplyModifiedPropertiesWithoutUndo();

            var g = new SerializedObject(game);
            Reference(g, "combatCastleMusic", creep);
            g.ApplyModifiedPropertiesWithoutUndo();

            var menuManager = UnityEngine.Object.FindAnyObjectByType<MagicTouch.UI.MenuManager>();
            if (menuManager == null) return;
            var m = new SerializedObject(menuManager);
            Reference(m, "menuMusic", menu);
            Reference(m, "combatMusic", creep);
            Reference(m, "gameVictorySound", victory);
            m.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindRoot(string name) =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == name);

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject NewObject(string name, Transform parent, params Type[] components)
        {
            var existing = parent != null ? parent.Find(name)?.gameObject : FindRoot(name);
            if (existing != null) return existing;
            var go = new GameObject(name, components);
            Undo.RegisterCreatedObjectUndo(go, "Rebuild Ember Drake boss");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = NewObject(name, parent, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // Solid colour bars: an Image without a sprite draws a plain rectangle.
        private static Image Solid(GameObject go, Color color)
        {
            var image = Component<Image>(go);
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Reference(SerializedObject target, string name, UnityEngine.Object value)
        {
            var property = target.FindProperty(name);
            if (property == null) throw new InvalidOperationException($"{target.targetObject.GetType().Name} has no field {name}.");
            property.objectReferenceValue = value;
        }

        // `??` skips Unity's fake-null, so a missing component must be tested with `!= null`.
        private static T Component<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
    }
}
