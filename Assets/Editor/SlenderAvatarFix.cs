using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sokophobia.Editor
{
    /// <summary>
    /// One-click fix for the NVJOB Slenderman's empty Humanoid mapping
    /// (Slender.fbx.meta ships animationType=Humanoid with human:[]/skeleton:[]).
    /// Builds a valid human avatar from the scene instance's bones (Blender-style
    /// names the auto-mapper doesn't know), saves it as an asset, and points the
    /// Sokobot Animator at it. Run once via Windoweless call:
    ///   Sokophobia.Editor.SlenderAvatarFix.Fix()
    /// Must run in Edit mode (not Play).
    /// </summary>
    public static class SlenderAvatarFix
    {
        // humanName -> boneName
        private static readonly KeyValuePair<string, string>[] Map =
        {
            new("Hips", "hips"),
            new("Spine", "spine"),
            new("Chest", "chest"),
            new("UpperChest", "chest1"),
            new("Neck", "neck"),
            new("Head", "head"),
            new("LeftEye", "eye.L"),
            new("RightEye", "eye.R"),
            new("LeftShoulder", "shoulder.L"),
            new("RightShoulder", "shoulder.R"),
            new("LeftUpperArm", "upper_arm.L"),
            new("LeftLowerArm", "forearm.L"),
            new("LeftHand", "hand.L"),
            new("RightUpperArm", "upper_arm.R"),
            new("RightLowerArm", "forearm.R"),
            new("RightHand", "hand.R"),
            new("LeftUpperLeg", "thigh.L"),
            new("LeftLowerLeg", "shin.L"),
            new("LeftFoot", "foot.L"),
            new("LeftToes", "toe.L"),
            new("RightUpperLeg", "thigh.R"),
            new("RightLowerLeg", "shin.R"),
            new("RightFoot", "foot.R"),
            new("RightToes", "toe.R"),
        };

        private const string AvatarPath = "Assets/#NVJOB Slender/Slender/Mesh/SlenderHuman.avatar";

        [MenuItem("Sokophobia/Fix Slender Avatar")]
        private static void FixFromMenu() => Fix();

        public static void Fix()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SlenderAvatarFix] Exit Play mode first.");
                return;
            }

            var bot = GameObject.Find("Sokobot");
            if (bot == null)
            {
                Debug.LogError("[SlenderAvatarFix] No 'Sokobot' in the open scene.");
                return;
            }

            Transform hips = null;
            foreach (var t in bot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "hips") { hips = t; break; }
            }
            if (hips == null)
            {
                Debug.LogError("[SlenderAvatarFix] No 'hips' bone under Sokobot (is SlenderBody parented?).");
                return;
            }

            // Skeleton = hips down (skips the scaled 'Slender ' model container).
            var bones = new List<Transform>();
            CollectBones(hips, bones);

            var skeleton = new SkeletonBone[bones.Count];
            var byName = new Dictionary<string, Transform>();
            for (int i = 0; i < bones.Count; i++)
            {
                skeleton[i] = new SkeletonBone
                {
                    name = bones[i].name,
                    position = bones[i].localPosition,
                    rotation = bones[i].localRotation,
                    scale = bones[i].localScale,
                };
                if (!byName.ContainsKey(bones[i].name))
                    byName.Add(bones[i].name, bones[i]);
            }

            var human = new List<HumanBone>();
            foreach (var kv in Map)
            {
                if (!byName.ContainsKey(kv.Value))
                {
                    Debug.LogWarning($"[SlenderAvatarFix] Bone '{kv.Value}' not found, skipping {kv.Key}.");
                    continue;
                }
                human.Add(new HumanBone
                {
                    humanName = kv.Key,
                    boneName = kv.Value,
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }

            var desc = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton,
                armStretch = 0.05f,
                legStretch = 0.05f,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(hips.gameObject, desc);
            if (avatar == null || !avatar.isValid)
            {
                Debug.LogError("[SlenderAvatarFix] AvatarBuilder produced an invalid avatar. " +
                               "Check that Hips/Legs/Spine/Arms all mapped.");
                return;
            }

            // Replace any previous attempt so GUIDs stay stable.
            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(avatar, existing);
                avatar = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(avatar, AvatarPath);
            }

            var anim = bot.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                Debug.LogError("[SlenderAvatarFix] No Animator under Sokobot.");
                return;
            }
            anim.avatar = avatar;
            EditorUtility.SetDirty(anim);
            EditorSceneManager.MarkSceneDirty(anim.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SlenderAvatarFix] OK valid={avatar.isValid} human={avatar.isHuman} " +
                      $"bones={skeleton.Length} mapped={human.Count} -> {AvatarPath}");
        }

        private static void CollectBones(Transform t, List<Transform> outBones)
        {
            outBones.Add(t);
            for (int i = 0; i < t.childCount; i++)
                CollectBones(t.GetChild(i), outBones);
        }
    }
}
