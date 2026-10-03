using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MagicTouch.Gameplay;
using MagicTouch.Recognition;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Editor utility to automatically construct and configure the EnemyBalloon Prefab
    /// and connect sprite assets with 1-click in the Unity Editor.
    /// </summary>
    public static class MagicTouchPrefabBuilder
    {
        [MenuItem("Tools/Magic Touch/Create Default Enemy Prefab")]
        public static void CreateEnemyPrefab()
        {
            // 1. Ensure folders exist
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            // 2. Create Root Object
            GameObject root = new GameObject("EnemyBalloon");
            EnemyBalloon enemyComp = root.AddComponent<EnemyBalloon>();

            // 3. Child: Balloon Body
            GameObject balloonObj = new GameObject("BalloonBody");
            balloonObj.transform.SetParent(root.transform);
            balloonObj.transform.localPosition = Vector3.zero;
            SpriteRenderer balloonSr = balloonObj.AddComponent<SpriteRenderer>();
            balloonSr.sortingOrder = 1;

            Sprite balloonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Balloons/balloon_white.png");
            if (balloonSprite != null)
            {
                balloonSr.sprite = balloonSprite;
            }

            // 4. Child: Symbol Glyph Display
            GameObject symbolObj = new GameObject("SymbolDisplay");
            symbolObj.transform.SetParent(balloonObj.transform);
            // Center glyph inside the balloon sphere
            symbolObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            SpriteRenderer symbolSr = symbolObj.AddComponent<SpriteRenderer>();
            symbolSr.sortingOrder = 2;

            Sprite defaultSymbolSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Symbols/symbol_v.png");
            if (defaultSymbolSprite != null)
            {
                symbolSr.sprite = defaultSymbolSprite;
            }

            // 5. Child: Enemy Body (e.g. Frog or Knight) hanging below the string
            GameObject enemyBodyObj = new GameObject("EnemyBody");
            enemyBodyObj.transform.SetParent(root.transform);
            // Place below the balloon string
            enemyBodyObj.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            SpriteRenderer enemySr = enemyBodyObj.AddComponent<SpriteRenderer>();
            enemySr.sortingOrder = 0;

            Sprite frogSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Enemies/enemy_frog.png");
            if (frogSprite != null)
            {
                enemySr.sprite = frogSprite;
            }

            // 6. Connect Serialized References via SerializedObject
            SerializedObject so = new SerializedObject(enemyComp);
            so.FindProperty("_symbolRenderer").objectReferenceValue = symbolSr;
            so.FindProperty("_balloonRenderer").objectReferenceValue = balloonSr;

            Sprite popLeft = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/balloon_pop_left.png");
            Sprite popRight = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/balloon_pop_right.png");
            Sprite popSparkle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/pop_sparkle.png");
            if (popLeft != null) so.FindProperty("popLeftSprite").objectReferenceValue = popLeft;
            if (popRight != null) so.FindProperty("popRightSprite").objectReferenceValue = popRight;
            if (popSparkle != null) so.FindProperty("popSparkleSprite").objectReferenceValue = popSparkle;

            so.ApplyModifiedProperties();

            // 7. Save as Prefab
            string prefabPath = "Assets/Prefabs/EnemyBalloon.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[MagicTouchPrefabBuilder] Successfully created EnemyBalloon prefab at '{prefabPath}'!</color>");
            Selection.activeObject = savedPrefab;
        }

        [MenuItem("Tools/Magic Touch/Configure EnemySpawner Visuals in Scene")]
        public static void ConfigureEnemySpawnerInScene()
        {
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[MagicTouchPrefabBuilder] No EnemySpawner found in the active scene. Please create an EnemySpawner GameObject first.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);

            // Assign Prefab if not assigned
            SerializedProperty prefabProp = so.FindProperty("_enemyPrefab");
            if (prefabProp.objectReferenceValue == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyBalloon.prefab");
                if (prefab != null)
                {
                    prefabProp.objectReferenceValue = prefab;
                }
            }

            // Configure Symbol Visuals mapping (Symbol, Sprite, Balloon Tint)
            SerializedProperty visualsProp = so.FindProperty("_symbolVisuals");
            visualsProp.ClearArray();

            var mapping = new (GestureSymbol symbol, string spriteName, Color tint)[]
            {
                (GestureSymbol.V, "symbol_v", new Color(1f, 0.85f, 0.2f)),            // Yellow
                (GestureSymbol.Caret, "symbol_caret", new Color(0.25f, 0.6f, 1f)),     // Blue
                (GestureSymbol.HorizontalLine, "symbol_horizontal", new Color(0.2f, 0.85f, 0.9f)), // Cyan
                (GestureSymbol.VerticalLine, "symbol_vertical", new Color(1f, 0.35f, 0.7f)),       // Pink
                (GestureSymbol.O, "symbol_o", new Color(1f, 0.55f, 0.15f)),           // Orange
                (GestureSymbol.Z, "symbol_z", new Color(0.95f, 0.25f, 0.3f)),          // Red
                (GestureSymbol.W, "symbol_w", new Color(0.7f, 0.3f, 0.95f)),          // Purple
                (GestureSymbol.S, "symbol_s", new Color(0.25f, 0.85f, 0.35f)),        // Green
                (GestureSymbol.E, "symbol_e", new Color(0.85f, 0.3f, 0.5f)),          // Rose
                (GestureSymbol.X, "symbol_x", new Color(0.95f, 0.25f, 0.3f))          // Red
            };

            for (int i = 0; i < mapping.Length; i++)
            {
                visualsProp.InsertArrayElementAtIndex(i);
                SerializedProperty elem = visualsProp.GetArrayElementAtIndex(i);

                elem.FindPropertyRelative("symbol").enumValueIndex = (int)mapping[i].symbol;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Symbols/{mapping[i].spriteName}.png");
                elem.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                elem.FindPropertyRelative("balloonTint").colorValue = mapping[i].tint;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
            Debug.Log("<color=green>[MagicTouchPrefabBuilder] Configured EnemySpawner visual mapping with all 10 symbols and color tints!</color>");
        }
    }
}
