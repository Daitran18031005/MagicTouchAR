using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Visual Pop Effect for balloons in Magic Touch:
    /// - Spawns left and right popped balloon pieces (balloon_pop_left, balloon_pop_right).
    /// - Tints the pieces with the exact color of the popped balloon.
    /// - Emits bursting sparkle particles (pop_sparkle) that spin and fade.
    /// - Self-animates and destroys itself when the effect completes.
    /// </summary>
    public class BalloonPopEffect : MonoBehaviour
    {
        [Header("Pop Sprites")]
        [Tooltip("Left torn piece of the balloon.")]
        public Sprite popLeftSprite;

        [Tooltip("Right torn piece of the balloon.")]
        public Sprite popRightSprite;

        [Tooltip("Sparkle star sprite.")]
        public Sprite popSparkleSprite;

        [Header("Animation Settings")]
        [Tooltip("Total duration of the pop effect in seconds.")]
        public float duration = 0.5f;

        [Tooltip("Horizontal burst force of the balloon pieces.")]
        public float pieceForce = 3.5f;

        [Tooltip("Number of sparkles to emit.")]
        public int sparkleCount = 8;

        [Tooltip("Radial speed of sparkles.")]
        public float sparkleSpeed = 3.8f;

        /// <summary>
        /// Instantiates a BalloonPopEffect at worldPos with specified balloon tint and sprites.
        /// </summary>
        public static BalloonPopEffect Spawn(Vector3 worldPos, Color balloonColor, Sprite popLeft = null, Sprite popRight = null, Sprite sparkle = null)
        {
            GameObject popObj = new GameObject("BalloonPopEffect");
            popObj.transform.position = worldPos;

            BalloonPopEffect effect = popObj.AddComponent<BalloonPopEffect>();
            effect.popLeftSprite = popLeft;
            effect.popRightSprite = popRight;
            effect.popSparkleSprite = sparkle;

            effect.Initialize(balloonColor);
            return effect;
        }

        public void Initialize(Color balloonColor)
        {
            // Auto-fallback sprites if not assigned
            if (popLeftSprite == null) popLeftSprite = LoadSpriteResourceOrAsset("balloon_pop_left");
            if (popRightSprite == null) popRightSprite = LoadSpriteResourceOrAsset("balloon_pop_right");
            if (popSparkleSprite == null) popSparkleSprite = LoadSpriteResourceOrAsset("pop_sparkle");

            StartCoroutine(AnimatePopRoutine(balloonColor));
        }

        private IEnumerator AnimatePopRoutine(Color balloonColor)
        {
            // 1. Create Left Pop Piece
            GameObject leftObj = new GameObject("Piece_Left");
            leftObj.transform.SetParent(transform, false);
            leftObj.transform.localPosition = new Vector3(-0.15f, 0.05f, 0f);
            SpriteRenderer leftSr = leftObj.AddComponent<SpriteRenderer>();
            leftSr.sprite = popLeftSprite;
            leftSr.color = balloonColor;
            leftSr.sortingOrder = 10;
            Vector2 leftVel = new Vector2(-pieceForce, Random.Range(1.0f, 2.5f));

            // 2. Create Right Pop Piece
            GameObject rightObj = new GameObject("Piece_Right");
            rightObj.transform.SetParent(transform, false);
            rightObj.transform.localPosition = new Vector3(0.15f, 0.05f, 0f);
            SpriteRenderer rightSr = rightObj.AddComponent<SpriteRenderer>();
            rightSr.sprite = popRightSprite;
            rightSr.color = balloonColor;
            rightSr.sortingOrder = 10;
            Vector2 rightVel = new Vector2(pieceForce, Random.Range(1.0f, 2.5f));

            // 3. Create Sparkles
            List<Transform> sparkleTransforms = new List<Transform>();
            List<SpriteRenderer> sparkleRenderers = new List<SpriteRenderer>();
            List<Vector2> sparkleVelocities = new List<Vector2>();

            Color sparkleColor = Color.Lerp(Color.white, balloonColor, 0.25f);
            sparkleColor.a = 1.0f;

            for (int i = 0; i < sparkleCount; i++)
            {
                GameObject spObj = new GameObject($"Sparkle_{i}");
                spObj.transform.SetParent(transform, false);
                spObj.transform.localPosition = Vector3.zero;

                float angle = (i * (360f / sparkleCount) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = sparkleSpeed * Random.Range(0.75f, 1.25f);

                SpriteRenderer spSr = spObj.AddComponent<SpriteRenderer>();
                spSr.sprite = popSparkleSprite;
                spSr.color = sparkleColor;
                spSr.sortingOrder = 12;

                spObj.transform.localScale = Vector3.zero;
                spObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

                sparkleTransforms.Add(spObj.transform);
                sparkleRenderers.Add(spSr);
                sparkleVelocities.Add(dir * speed);
            }

            // 4. Animate over duration
            float elapsed = 0f;
            float gravity = 9.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Move and spin left piece
                leftVel.y -= gravity * Time.deltaTime;
                if (leftObj != null)
                {
                    leftObj.transform.localPosition += (Vector3)leftVel * Time.deltaTime;
                    leftObj.transform.Rotate(0, 0, -280f * Time.deltaTime);
                    float alpha = 1f - t;
                    float scale = 1f - t * 0.4f;
                    leftSr.color = new Color(balloonColor.r, balloonColor.g, balloonColor.b, alpha);
                    leftObj.transform.localScale = new Vector3(scale, scale, 1f);
                }

                // Move and spin right piece
                rightVel.y -= gravity * Time.deltaTime;
                if (rightObj != null)
                {
                    rightObj.transform.localPosition += (Vector3)rightVel * Time.deltaTime;
                    rightObj.transform.Rotate(0, 0, 280f * Time.deltaTime);
                    float alpha = 1f - t;
                    float scale = 1f - t * 0.4f;
                    rightSr.color = new Color(balloonColor.r, balloonColor.g, balloonColor.b, alpha);
                    rightObj.transform.localScale = new Vector3(scale, scale, 1f);
                }

                // Sparkles expand, spin, then fade
                for (int i = 0; i < sparkleTransforms.Count; i++)
                {
                    if (sparkleTransforms[i] != null)
                    {
                        sparkleVelocities[i] *= Mathf.Pow(0.1f, Time.deltaTime); // Drag
                        sparkleTransforms[i].localPosition += (Vector3)sparkleVelocities[i] * Time.deltaTime;
                        sparkleTransforms[i].Rotate(0, 0, 360f * Time.deltaTime);

                        // Scale up fast in first 20%, then shrink
                        float sScale;
                        if (t < 0.2f)
                        {
                            sScale = Mathf.Lerp(0f, 1.2f, t / 0.2f);
                        }
                        else
                        {
                            sScale = Mathf.Lerp(1.2f, 0f, (t - 0.2f) / 0.8f);
                        }
                        sparkleTransforms[i].localScale = new Vector3(sScale, sScale, 1f);

                        // Fade alpha
                        float sAlpha = 1f - t;
                        Color c = sparkleRenderers[i].color;
                        c.a = sAlpha;
                        sparkleRenderers[i].color = c;
                    }
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        private static Sprite LoadSpriteResourceOrAsset(string spriteName)
        {
#if UNITY_EDITOR
            string path = $"Assets/Sprites/VFX/{spriteName}.png";
            Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;
#endif
            return Resources.Load<Sprite>(spriteName);
        }
    }
}
